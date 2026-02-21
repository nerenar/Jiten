using System.Diagnostics;
using System.Text.RegularExpressions;
using Jiten.Core;
using Jiten.Core.Data;
using Jiten.Core.Data.JMDict;
using Jiten.Core.Utils;
using Jiten.Parser.Data.Redis;
using Jiten.Parser.Diagnostics;
using Jiten.Parser.Runtime;
using Jiten.Parser.Scoring;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using WanaKanaShaapu;

namespace Jiten.Parser
{
    public static class Parser
    {
        private static readonly bool UseCache = true;
        private static readonly bool UseRescue = true;
        private static readonly ParserRuntime _parserRuntime = new();
        private static IDeckWordCache DeckWordCache = null!;
        private static IJmDictCache JmDictCache = null!;

        private static IDbContextFactory<JitenDbContext> _contextFactory = null!;
        private static Dictionary<string, List<int>> _lookups = null!;
        private static HashSet<int> _nameOnlyWordIds = null!;

        // Cache for compound expression lookups with bounded eviction
        private static readonly Dictionary<string, (bool validExpression, int? wordId)> CompoundExpressionCache = new();
        private static readonly LinkedList<string> CompoundCacheOrder = new(); // Tracks insertion order for LRU eviction
        private static readonly Lock CompoundCacheLock = new();
        private const int MAX_COMPOUND_CACHE_SIZE = 200_000;
        private const int EVICTION_BATCH_SIZE = 50_000; // Remove oldest 25% when limit hit

        // Compiled regexes for token cleaning (avoid recompilation on every token)
        private static readonly Regex TokenCleanRegex = new(
                                                            @"[^a-zA-Z0-9\u3040-\u309F\u30A0-\u30FF\u4E00-\u9FAF\uFF21-\uFF3A\uFF41-\uFF5A\uFF10-\uFF19\u3005．]",
                                                            RegexOptions.Compiled);

        private static readonly Regex SmallTsuLongVowelRegex = new(@"ッー", RegexOptions.Compiled);

        // Surface-text misparse tokens to remove after repair stages (deferred from MorphologicalAnalyser
        // so RepairLongVowelMisparses can use them for backward-merge reconstruction)
        private static readonly HashSet<string> MisparsesRemove =
        [
            "そ", "る", "ま", "ふ", "ち", "ほ", "す", "じ", "なさ", "い", "ぴ", "ふあ", "ぷ", "ちゅ", "にっ", "じら", "タ", "け", "イ", "イッ", "ほっ", "そっ",
            "ウー", "うー", "ううう", "うう", "ウウウウ", "ウウ", "ううっ", "かー", "ぐわー", "違", "タ", "ッ"
        ];

        // Excluded (WordId, ReadingIndex) pairs to filter from final parsing results
        private static readonly HashSet<(int WordId, byte ReadingIndex)> ExcludedMisparses =
        [
            (1291070, 1), (1587980, 1), (1443970, 5), (2029660, 0), (1177490, 5), (2029000, 1),
            (1244950, 1), (1243940, 1), (2747970, 1), (2029680, 0), (1193570, 6), (1796500, 2),
            (1811220, 1), (2654270, 0), (2269410, 1), (2439040, 3), (2861095, 0), (2836250, 0),
            (1595910, 4), (2577750, 0), (1365520, 1), (1310720, 1), (1528180,1), (2866457,1),
            (2394370,4), (1203250,2), (1537250,2), (2783750,1), (2654250,0), (2609820,1),
            (2080360,3), (1333240,2), (2035220,2), (5616612,5), (2249020,1), (2783700,1),
            (2411420,0)
        ];

        public static async Task WarmupAsync(IDbContextFactory<JitenDbContext> contextFactory)
        {
            await EnsureInitializedAsync(contextFactory);
        }

        private static async Task EnsureInitializedAsync(IDbContextFactory<JitenDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            var runtime = await _parserRuntime.EnsureInitializedAsync(contextFactory);
            DeckWordCache = runtime.DeckWordCache;
            JmDictCache = runtime.JmDictCache;
            _lookups = runtime.Lookups;
            _nameOnlyWordIds = runtime.NameOnlyWordIds;
        }

        private static async Task PreprocessSentences(List<SentenceInfo> sentences)
        {
            CleanSentenceTokens(sentences);
            SplitSuruInflectionsForNounCompounding(sentences);
            SplitUnknownNounTokens(sentences);
            CombineNounCompounds(sentences);
            await CombineCompounds(sentences);
            RepairLongVowelMisparses(sentences);
            RepairLongVowelTokens(sentences);
            FilterOrphanedMisparses(sentences);
            MarkPersonNameHonorificContexts(sentences);
        }

        private static readonly HashSet<string> ArchaicPosTypes =
        [
            "v2a-s", "v2b-k", "v2b-s", "v2d-k", "v2d-s", "v2g-k", "v2g-s",
            "v2h-k", "v2h-s", "v2k-k", "v2k-s", "v2m-k", "v2m-s", "v2n-s",
            "v2r-k", "v2r-s", "v2s-s", "v2t-k", "v2t-s", "v2w-s", "v2y-k",
            "v2y-s", "v2z-s",
            "v4b", "v4g", "v4h", "v4k", "v4m", "v4n", "v4r", "v4s", "v4t",
            "adj-kari", "adj-ku", "adj-shiku"
        ];

        private static readonly HashSet<string> PersonHonorifics = ["さん", "ちゃん", "くん", "氏", "様"];

        private static readonly HashSet<string> HonorificExclusions =
        [
            "うさぎ", "ウサギ", "兎",
            "くま", "クマ", "熊",
            "たぬき", "タヌキ", "狸",
            "きつね", "キツネ", "狐",
            "さる", "サル", "猿",
            "つる", "ツル", "鶴",
            "かめ", "カメ", "亀",
            "ねずみ", "ネズミ", "鼠",
        ];

        private static void MarkPersonNameHonorificContexts(List<SentenceInfo> sentences)
        {
            foreach (var sentence in sentences)
            {
                if (sentence.Words.Count < 2)
                    continue;

                for (int i = 0; i < sentence.Words.Count - 1; i++)
                {
                    var current = sentence.Words[i].word;
                    var next = sentence.Words[i + 1].word;

                    if (!PersonHonorifics.Contains(next.Text))
                        continue;

                    if (HonorificExclusions.Contains(current.Text))
                        continue;

                    if (!PosMapper.IsNameLikeSudachiNoun(
                                                         current.PartOfSpeech,
                                                         current.PartOfSpeechSection1,
                                                         current.PartOfSpeechSection2,
                                                         current.PartOfSpeechSection3))
                        continue;

                    if (!current.HasPartOfSpeechSection(PartOfSpeechSection.PersonName) &&
                        !current.HasPartOfSpeechSection(PartOfSpeechSection.FamilyName) &&
                        !current.HasPartOfSpeechSection(PartOfSpeechSection.Name))
                        continue;

                    current.IsPersonNameContext = true;
                }
            }
        }

        private static void PropagatePersonNameContexts(List<List<SentenceInfo>> allTexts)
        {
            var confirmedNames = new HashSet<string>();

            foreach (var sentences in allTexts)
            foreach (var sentence in sentences)
            foreach (var (word, _, _) in sentence.Words)
                if (word.IsPersonNameContext)
                    confirmedNames.Add(word.Text);

            if (confirmedNames.Count == 0)
                return;

            foreach (var sentences in allTexts)
            foreach (var sentence in sentences)
            foreach (var (word, _, _) in sentence.Words)
            {
                if (word.IsPersonNameContext)
                    continue;

                if (!confirmedNames.Contains(word.Text))
                    continue;

                if (!PosMapper.IsNameLikeSudachiNoun(
                                                     word.PartOfSpeech,
                                                     word.PartOfSpeechSection1,
                                                     word.PartOfSpeechSection2,
                                                     word.PartOfSpeechSection3))
                    continue;

                if (!word.HasPartOfSpeechSection(PartOfSpeechSection.PersonName) &&
                    !word.HasPartOfSpeechSection(PartOfSpeechSection.FamilyName) &&
                    !word.HasPartOfSpeechSection(PartOfSpeechSection.Name))
                    continue;

                word.IsPersonNameContext = true;
            }
        }

        private static void PropagatePersonNameContexts(List<SentenceInfo> sentences)
        {
            PropagatePersonNameContexts(new List<List<SentenceInfo>> { sentences });
        }

        /// <summary>
        /// Sudachi + CombineInflections can merge a suru-noun with its inflection (e.g., 交換 + して → 交換して),
        /// which can prevent later noun compounding (e.g., 物々 + 交換して instead of 物々交換 + して).
        /// This pass selectively splits tokens like Xして into X + して ONLY when doing so enables a valid noun compound
        /// with preceding noun tokens (validated against the JMDict lookups table).
        /// </summary>
        private static void SplitSuruInflectionsForNounCompounding(List<SentenceInfo> sentences)
        {
            static string? GetSuruSplitSuffixPrefix(string surface)
            {
                // Keep this intentionally small to avoid creating standalone auxiliary chains.
                // We only need the core suru te-form/past forms for noun-compound recovery.
                // Note: we match as a PREFIX so this also works when the te-form is followed by auxiliaries
                // (e.g., 交換しておかない).
                if (surface.StartsWith("して", StringComparison.Ordinal)) return "して";
                if (surface.StartsWith("した", StringComparison.Ordinal)) return "した";
                if (surface.StartsWith("し", StringComparison.Ordinal)) return "し";
                return null;
            }

            static bool HasLookup(string key, Dictionary<string, List<int>> lookups)
            {
                if (lookups.TryGetValue(key, out var ids) && ids.Count > 0)
                    return true;

                var hiraganaKey = WanaKana.ToHiragana(key, new DefaultOptions { ConvertLongVowelMark = false });
                return hiraganaKey != key &&
                       lookups.TryGetValue(hiraganaKey, out ids) &&
                       ids.Count > 0;
            }

            foreach (var sentence in sentences)
            {
                if (sentence.Words.Count < 2)
                    continue;

                for (int i = 1; i < sentence.Words.Count; i++)
                {
                    var (word, position, length) = sentence.Words[i];

                    if (string.IsNullOrEmpty(word.DictionaryForm))
                        continue;

                    string baseNoun;
                    if (word.DictionaryForm.EndsWith("する", StringComparison.Ordinal) && word.DictionaryForm.Length > 2)
                    {
                        // e.g., 交換する / 勉強する
                        baseNoun = word.DictionaryForm[..^2];
                    }
                    else if (word.HasPartOfSpeechSection(PartOfSpeechSection.PossibleSuru))
                    {
                        // Some upstream combiners (e.g., CombineVerbDependantsSuru) keep DictionaryForm as the noun stem
                        // even when Text includes the suru inflection chain (e.g., 交換してる with DictionaryForm=交換).
                        baseNoun = word.DictionaryForm;
                    }
                    else
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(baseNoun))
                        continue;

                    if (!word.Text.StartsWith(baseNoun, StringComparison.Ordinal))
                        continue;

                    var suffixSurface = word.Text[baseNoun.Length..];
                    var allowedSuffix = GetSuruSplitSuffixPrefix(suffixSurface);
                    if (allowedSuffix == null)
                        continue;

                    // Find whether splitting enables a valid noun compound with preceding nouns
                    // Try longest window first: up to 4 preceding noun tokens + baseNoun = 5-token compound.
                    int bestStart = -1;
                    for (int windowSize = Math.Min(5, i + 1); windowSize >= 2; windowSize--)
                    {
                        int nounCount = windowSize - 1;
                        int start = i - nounCount;
                        if (start < 0) continue;

                        bool allNouns = true;
                        for (int j = start; j < i; j++)
                        {
                            if (!PosMapper.IsNounForCompounding(sentence.Words[j].word.PartOfSpeech))
                            {
                                allNouns = false;
                                break;
                            }
                        }

                        if (!allNouns) continue;

                        var prefix = string.Concat(sentence.Words.Skip(start).Take(nounCount).Select(w => w.word.Text));
                        var combined = prefix + baseNoun;
                        if (HasLookup(combined, _lookups))
                        {
                            bestStart = start;
                            break;
                        }
                    }

                    if (bestStart == -1)
                        continue;

                    // Split into base noun + suru inflection (+ optional tail).
                    // We intentionally avoid trying to preserve Sudachi readings here:
                    // merged tokens often carry only the accumulator reading, and suffixes may not be at the end.
                    var nounWord = new WordInfo(word)
                                   {
                                       Text = baseNoun, PartOfSpeech = PartOfSpeech.Noun, DictionaryForm = baseNoun,
                                       // Keep NormalizedForm from Sudachi (often important for matching); fall back to base noun.
                                       NormalizedForm = string.IsNullOrEmpty(word.NormalizedForm) ? baseNoun : word.NormalizedForm,
                                       Reading = WanaKana.ToHiragana(baseNoun, new DefaultOptions { ConvertLongVowelMark = false })
                                   };

                    var suruWord = new WordInfo
                                   {
                                       // Keep the full surface (e.g., しておかない / してる) as a single token.
                                       // This preserves auxiliary chains as one unit while enabling noun compounding.
                                       Text = suffixSurface, PartOfSpeech = PartOfSpeech.Verb, DictionaryForm = "する", NormalizedForm = "する",
                                       Reading = WanaKana.ToHiragana(suffixSurface, new DefaultOptions { ConvertLongVowelMark = false })
                                   };

                    int baseLen = nounWord.Text.Length;
                    int suffixLen = suruWord.Text.Length;

                    sentence.Words[i] = (nounWord, position, baseLen);
                    sentence.Words.Insert(i + 1, (suruWord, position + baseLen, suffixLen));
                    i++; // Skip inserted suffix
                }
            }
        }

        private static List<WordInfo> ExtractWordInfos(List<SentenceInfo> sentences)
        {
            return sentences.SelectMany(s => s.Words)
                            .Where(w => w.word.PartOfSpeech != PartOfSpeech.SupplementarySymbol)
                            .Select(w => w.word)
                            .ToList();
        }

        private static List<(WordInfo wordInfo, int occurrences)> CountUniqueWords(List<WordInfo> wordInfos)
        {
            var uniqueWords = new List<(WordInfo wordInfo, int occurrences)>();
            var wordCount =
                new Dictionary<(string text, PartOfSpeech pos, string reading, bool isPersonNameContext, bool isNameLikeSudachiNoun),
                    int>();

            foreach (var word in wordInfos)
            {
                var isNameLikeSudachiNoun = PosMapper.IsNameLikeSudachiNoun(word.PartOfSpeech, word.PartOfSpeechSection1,
                                                                            word.PartOfSpeechSection2, word.PartOfSpeechSection3);
                var key = (word.Text, word.PartOfSpeech, word.Reading, word.IsPersonNameContext, isNameLikeSudachiNoun);

                if (!wordCount.TryAdd(key, 1))
                {
                    wordCount[key]++;
                }
                else
                {
                    uniqueWords.Add((word, 1));
                }
            }

            for (int i = 0; i < uniqueWords.Count; i++)
            {
                var wi = uniqueWords[i].wordInfo;
                var isNameLikeSudachiNoun = PosMapper.IsNameLikeSudachiNoun(wi.PartOfSpeech, wi.PartOfSpeechSection1,
                                                                            wi.PartOfSpeechSection2, wi.PartOfSpeechSection3);
                var key = (wi.Text, wi.PartOfSpeech, wi.Reading, wi.IsPersonNameContext, isNameLikeSudachiNoun);
                uniqueWords[i] = (uniqueWords[i].wordInfo, wordCount[key]);
            }

            return uniqueWords;
        }

        private static async Task<List<DeckWord?>> ProcessWordsInBatches(
            List<(WordInfo wordInfo, int occurrences)> words,
            Deconjugator deconjugator,
            bool applyRescue = true,
            int batchSize = 1000,
            ParserDiagnostics? diagnostics = null)
        {
            List<DeckWord?> allProcessedWords = new();
            List<(int index, WordInfo wordInfo, int occurrences)> failedWords = new();

            int globalIndex = 0;
            for (int i = 0; i < words.Count; i += batchSize)
            {
                var batch = words.Skip(i).Take(batchSize).ToList();
                var processBatch = batch.Select(word => ProcessWord(word, deconjugator, diagnostics)).ToList();
                var batchResults = await Task.WhenAll(processBatch);

                for (int j = 0; j < batch.Count; j++)
                {
                    allProcessedWords.Add(batchResults[j].Word);
                    if (applyRescue && batchResults[j].ShouldRescue)
                        failedWords.Add((globalIndex, batch[j].wordInfo, batch[j].occurrences));
                    globalIndex++;
                }
            }

            if (applyRescue && UseRescue && failedWords.Count > 0)
            {
                diagnostics?.RunSummary.AddRescueInvocations(failedWords.Count);
                diagnostics?.RunSummary.AddRescueCandidates(failedWords.Count);

                var rescueInput = failedWords.Select(f => (f.wordInfo, f.occurrences)).ToList();
                var rescuedGroups = await RescueFailedWords(rescueInput, deconjugator);

                int offset = 0;
                for (int i = 0; i < failedWords.Count; i++)
                {
                    var adjustedIndex = failedWords[i].index + offset;
                    var rescuedGroup = rescuedGroups[i];

                    if (rescuedGroup.Count > 0)
                        diagnostics?.RunSummary.IncrementRescueRecoveredCount();
                    else
                        diagnostics?.RunSummary.IncrementRescueUnresolvedCount();

                    allProcessedWords.RemoveAt(adjustedIndex);
                    allProcessedWords.InsertRange(adjustedIndex, rescuedGroup);
                    offset += (rescuedGroup.Count - 1);
                }
            }

            return allProcessedWords;
        }

        public static async Task<List<DeckWord>> ParseText(IDbContextFactory<JitenDbContext> contextFactory, string text,
                                                           bool preserveStopToken = false,
                                                           ParserDiagnostics? diagnostics = null)
        {
            await EnsureInitializedAsync(contextFactory);

            var parser = new MorphologicalAnalyser { HasCompoundLookup = HasLookupForCompound };
            var sentences = await parser.Parse(text, preserveStopToken: preserveStopToken, diagnostics: diagnostics);

            await PreprocessSentences(sentences);
            PropagatePersonNameContexts(sentences);
            var wordInfos = ExtractWordInfos(sentences);
            var wordsWithOccurrences = wordInfos.Select(w => (w, 0)).ToList();

            var processed = await ProcessWordsInBatches(wordsWithOccurrences, Deconjugator.Instance, diagnostics: diagnostics);
            return ExcludeFinalMisparses(processed.Where(w => w != null).Select(w => w!));
        }

        /// <summary>
        /// Parses a single text into a Deck. Delegates to ParseTextsToDeck for a single codepath.
        /// </summary>
        public static async Task<Deck> ParseTextToDeck(IDbContextFactory<JitenDbContext> contextFactory, string text,
                                                       bool storeRawText = false,
                                                       bool predictDifficulty = true,
                                                       MediaType mediatype = MediaType.Novel,
                                                       ParserDiagnostics? diagnostics = null)
        {
            var results = await ParseTextsToDeck(contextFactory, [text], storeRawText, predictDifficulty, mediatype, diagnostics);
            return results.Count > 0 ? results[0] : new Deck();
        }

        /// <summary>
        /// Parses multiple texts into Decks in a single batch for efficiency.
        /// </summary>
        public static async Task<List<Deck>> ParseTextsToDeck(IDbContextFactory<JitenDbContext> contextFactory,
                                                              List<string> texts,
                                                              bool storeRawText = false,
                                                              bool predictDifficulty = true,
                                                              MediaType mediatype = MediaType.Novel,
                                                              ParserDiagnostics? diagnostics = null)
        {
            if (texts.Count == 0) return [];

            await EnsureInitializedAsync(contextFactory);

            var timer = new Stopwatch();
            timer.Start();

            // Batch morphological analysis
            var parser = new MorphologicalAnalyser { HasCompoundLookup = HasLookupForCompound };
            var batchedSentences = await parser.ParseBatch(texts, diagnostics: diagnostics);

            timer.Stop();
            double sudachiTime = timer.Elapsed.TotalMilliseconds;
            Console.WriteLine($"Batch parsed {texts.Count} texts. Sudachi time: {sudachiTime:0.0}ms");

            timer.Restart();

            // Pass 1: Preprocess all texts, then propagate person names across all texts
            for (int textIndex = 0; textIndex < batchedSentences.Count; textIndex++)
                await PreprocessSentences(batchedSentences[textIndex]);

            PropagatePersonNameContexts(batchedSentences);

            // Pass 2: Process each preprocessed result through deconjugation/lookup pipeline
            var decks = new List<Deck>();
            Deconjugator deconjugator = Deconjugator.Instance;

            for (int textIndex = 0; textIndex < batchedSentences.Count; textIndex++)
            {
                var sentences = batchedSentences[textIndex];
                var text = texts[textIndex];
                var deck = await ProcessSentencesToDeck(sentences, text, deconjugator, storeRawText, predictDifficulty, mediatype);
                decks.Add(deck);
            }

            timer.Stop();
            Console.WriteLine($"Processing time: {timer.Elapsed.TotalMilliseconds:0.0}ms");

            return decks;
        }

        /// <summary>
        /// Helper: Processes sentences into a Deck (deconjugation, rescue, statistics).
        /// Extracted to share between batch and single-item paths.
        /// </summary>
        private static async Task<Deck> ProcessSentencesToDeck(
            List<SentenceInfo> sentences,
            string text,
            Deconjugator deconjugator,
            bool storeRawText,
            bool predictDifficulty,
            MediaType mediatype)
        {
            var wordInfos = ExtractWordInfos(sentences);
            var uniqueWords = CountUniqueWords(wordInfos);

            var allProcessedWords = await ProcessWordsInBatches(uniqueWords, deconjugator);
            var processedWords = allProcessedWords.Where(w => w != null).Select(w => w!).ToArray();

            // Sum occurrences while deduplicating by WordId and ReadingIndex for deconjugated words
            processedWords = processedWords
                             .GroupBy(x => new { x.WordId, x.ReadingIndex })
                             .Select(g =>
                             {
                                 var first = g.First();
                                 first.Occurrences = g.Sum(w => w.Occurrences);
                                 return first;
                             })
                             .ToArray();

            processedWords = ExcludeFinalMisparses(processedWords).ToArray();

            List<ExampleSentence>? exampleSentences = null;

            if (mediatype is MediaType.Novel or MediaType.NonFiction or MediaType.VideoGame or MediaType.VisualNovel or MediaType.WebNovel)
                exampleSentences = ExampleSentenceExtractor.ExtractSentences(sentences, processedWords);

            var totalWordCount = processedWords.Select(w => w.Occurrences).Sum();
            var characterCount = wordInfos.Sum(x => x.Text.Length);

            var textWithoutDialogues = Regex.Replace(text, @"[「『].{0,200}?[」』]", "", RegexOptions.Singleline);
            textWithoutDialogues = Regex.Replace(textWithoutDialogues,
                                                 "[^a-zA-Z0-9\u3040-\u309F\u30A0-\u30FF\u4E00-\u9FAF\uFF21-\uFF3A\uFF41-\uFF5A\uFF10-\uFF19\u3005．]",
                                                 "");
            var textWithoutPunctuation = Regex.Replace(text,
                                                       "[^a-zA-Z0-9\u3040-\u309F\u30A0-\u30FF\u4E00-\u9FAF\uFF21-\uFF3A\uFF41-\uFF5A\uFF10-\uFF19\u3005．]",
                                                       "");

            int dialogueCharacterCount = textWithoutPunctuation.Length - textWithoutDialogues.Length;
            float dialoguePercentage = textWithoutPunctuation.Length > 0
                ? (float)dialogueCharacterCount / textWithoutPunctuation.Length * 100f
                : 0f;

            var deck = new Deck
                       {
                           CharacterCount = characterCount, WordCount = totalWordCount, UniqueWordCount = processedWords.Length,
                           UniqueWordUsedOnceCount = processedWords.Count(x => x.Occurrences == 1),
                           UniqueKanjiCount = wordInfos.SelectMany(w => w.Text).Distinct().Count(c => WanaKana.IsKanji(c.ToString())),
                           UniqueKanjiUsedOnceCount = wordInfos.SelectMany(w => w.Text).GroupBy(c => c)
                                                               .Count(g => g.Count() == 1 && WanaKana.IsKanji(g.Key.ToString())),
                           SentenceCount = sentences.Count, DialoguePercentage = dialoguePercentage, DeckWords = processedWords,
                           RawText = storeRawText ? new DeckRawText(text) : null, ExampleSentences = exampleSentences
                       };

            return deck;
        }

        public static async Task<List<DeckWord?>> ParseMorphenes(IDbContextFactory<JitenDbContext> contextFactory, string text,
                                                                 ParserDiagnostics? diagnostics = null)
        {
            await EnsureInitializedAsync(contextFactory);

            var parser = new MorphologicalAnalyser { HasCompoundLookup = HasLookupForCompound };
            var sentences = await parser.Parse(text, morphemesOnly: true, diagnostics: diagnostics);
            var wordInfos = sentences.SelectMany(s => s.Words).Select(w => w.word).ToList();

            wordInfos.ForEach(x => x.Text = SmallTsuLongVowelRegex.Replace(x.Text, ""));
            var wordsWithOccurrences = wordInfos.Select(w => (w, 0)).ToList();

            var processed = await ProcessWordsInBatches(wordsWithOccurrences, Deconjugator.Instance, applyRescue: false, batchSize: 5000);

            return processed
                   .Where(w => w == null || !ExcludedMisparses.Contains((w.WordId, w.ReadingIndex)))
                   .ToList();
        }

        // Limit how many concurrent operations we perform to prevent overwhelming the system
        private static readonly SemaphoreSlim _processSemaphore = new SemaphoreSlim(100, 100);

        private enum ProcessWordStatus
        {
            Resolved,
            FilteredOut,
            TimedOut,
            Unresolved
        }

        private readonly struct ProcessWordResult
        {
            private ProcessWordResult(ProcessWordStatus status, DeckWord? word = null)
            {
                Status = status;
                Word = word;
            }

            public ProcessWordStatus Status { get; }
            public DeckWord? Word { get; }
            public bool ShouldRescue => Status is ProcessWordStatus.TimedOut or ProcessWordStatus.Unresolved;

            public static ProcessWordResult FromResolved(DeckWord word) => new(ProcessWordStatus.Resolved, word);
            public static ProcessWordResult FilteredOut { get; } = new(ProcessWordStatus.FilteredOut);
            public static ProcessWordResult TimedOut { get; } = new(ProcessWordStatus.TimedOut);
            public static ProcessWordResult Unresolved { get; } = new(ProcessWordStatus.Unresolved);
        }

        private static async Task<ProcessWordResult> ProcessWord((WordInfo wordInfo, int occurrences) wordData, Deconjugator deconjugator,
                                                                 ParserDiagnostics? diagnostics = null)
        {
            // Try to acquire semaphore with timeout to prevent deadlock
            if (!await _processSemaphore.WaitAsync(TimeSpan.FromSeconds(5)))
            {
                // If we can't get the semaphore in a reasonable time, surface an explicit timeout status.
                // This is better than hanging indefinitely
                diagnostics?.RunSummary.IncrementProcessSemaphoreTimeoutCount();
                diagnostics?.RunSummary.IncrementUnresolvedTokenCount();
                return ProcessWordResult.TimedOut;
            }

            try
            {
                // Early check for digits before anything else (including cache lookup)
                var textWithoutBar = wordData.wordInfo.Text.TrimEnd('ー');
                if (textWithoutBar.Length > 0 && textWithoutBar.All(char.IsDigit) ||
                    (textWithoutBar.Length == 1 && textWithoutBar.IsAsciiOrFullWidthLetter()))
                {
                    return ProcessWordResult.FilteredOut;
                }

                var isNameLikeSudachiNoun = PosMapper.IsNameLikeSudachiNoun(
                                                                            wordData.wordInfo.PartOfSpeech,
                                                                            wordData.wordInfo.PartOfSpeechSection1,
                                                                            wordData.wordInfo.PartOfSpeechSection2,
                                                                            wordData.wordInfo.PartOfSpeechSection3);

                var cacheKey = new DeckWordCacheKey(
                                                    wordData.wordInfo.Text,
                                                    wordData.wordInfo.PartOfSpeech,
                                                    wordData.wordInfo.DictionaryForm,
                                                    wordData.wordInfo.Reading,
                                                    wordData.wordInfo.IsPersonNameContext,
                                                    isNameLikeSudachiNoun
                                                   );

                if (UseCache && diagnostics == null)
                {
                    try
                    {
                        var cachedWord = await DeckWordCache.GetAsync(cacheKey);

                        if (cachedWord != null && cachedWord.WordId != -1)
                        {
                            return ProcessWordResult.FromResolved(
                                new DeckWord
                                {
                                    WordId = cachedWord.WordId, OriginalText = wordData.wordInfo.Text,
                                    ReadingIndex = cachedWord.ReadingIndex, Occurrences = wordData.occurrences,
                                    Conjugations = cachedWord.Conjugations, PartsOfSpeech = cachedWord.PartsOfSpeech,
                                    Origin = cachedWord.Origin, SudachiReading = wordData.wordInfo.Reading
                                });
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log but continue - we'll just process the word directly
                        Console.WriteLine($"[Warning] Failed to read from DeckWordCache: {ex.Message}");
                    }
                }

                DeckWord? processedWord = null;
                bool isProcessed = false;
                int attemptCount = 0;
                const int maxAttempts = 3; // Limit how many attempts we make to prevent infinite loops

                var baseWord = wordData.wordInfo.Text;
                do
                {
                    attemptCount++;
                    try
                    {
                        // If the word has a pre-matched wordId (e.g. from CombineCompounds), use it directly
                        if (wordData.wordInfo.PreMatchedWordId.HasValue)
                        {
                            var preMatchedWordId = wordData.wordInfo.PreMatchedWordId.Value;
                            var wordCache = await JmDictCache.GetWordsAsync([preMatchedWordId]);
                            if (wordCache.TryGetValue(preMatchedWordId, out var preMatchedWord))
                            {
                                // Use DictionaryForm for reading lookup since Text may be conjugated (e.g. そうしよう vs そうする)
                                var textForReadingLookup = !string.IsNullOrEmpty(wordData.wordInfo.DictionaryForm)
                                    ? wordData.wordInfo.DictionaryForm
                                    : wordData.wordInfo.Text;
                                var readingIndex = GetBestReadingIndex(preMatchedWord, textForReadingLookup);
                                processedWord = new DeckWord
                                                {
                                                    WordId = preMatchedWordId, ReadingIndex = readingIndex,
                                                    OriginalText = wordData.wordInfo.Text, Occurrences = wordData.occurrences,
                                                    PartsOfSpeech = preMatchedWord.PartsOfSpeech.ToPartOfSpeech(),
                                                    Origin = preMatchedWord.Origin
                                                };
                                break;
                            }
                        }

                        if (wordData.wordInfo.PartOfSpeech is PartOfSpeech.Verb or PartOfSpeech.IAdjective or PartOfSpeech.Auxiliary
                                or PartOfSpeech.NaAdjective or PartOfSpeech.Expression ||
                            wordData.wordInfo.PartOfSpeechSection1 is PartOfSpeechSection.Adjectival)
                        {
                            // Try to deconjugate as verb or adjective
                            var verbResult = await DeconjugateVerbOrAdjective(wordData, deconjugator, diagnostics);
                            if (!verbResult.success || verbResult.word == null)
                            {
                                // The word might be a noun misparsed as a verb/adjective like お祭り
                                var nounResult = await DeconjugateWord(wordData, diagnostics);
                                processedWord = nounResult.word;
                            }
                            else
                            {
                                processedWord = verbResult.word;
                            }
                        }
                        else
                        {
                            var nounResult = await DeconjugateWord(wordData, diagnostics);
                            if (!nounResult.success || nounResult.word == null)
                            {
                                // The word might be a conjugated noun + suru
                                var verbResult = await DeconjugateVerbOrAdjective(wordData, deconjugator, diagnostics);

                                var oldPos = wordData.wordInfo.PartOfSpeech;
                                // The word might be a verb or an adjective misparsed as a noun like らしく
                                if (!verbResult.success || verbResult.word == null)
                                {
                                    wordData.wordInfo.PartOfSpeech = PartOfSpeech.Verb;
                                    verbResult = await DeconjugateVerbOrAdjective(wordData, deconjugator, diagnostics);
                                }

                                if (!verbResult.success || verbResult.word == null)
                                {
                                    wordData.wordInfo.PartOfSpeech = PartOfSpeech.IAdjective;
                                    verbResult = await DeconjugateVerbOrAdjective(wordData, deconjugator, diagnostics);
                                }

                                if (!verbResult.success || verbResult.word == null)
                                {
                                    wordData.wordInfo.PartOfSpeech = PartOfSpeech.NaAdjective;
                                    // Prefer direct lookup for na-adjective stems that Sudachi may label as nouns/names (e.g., 朧気).
                                    var naDirect = await DeconjugateWord(wordData, diagnostics);
                                    if (naDirect.success && naDirect.word != null)
                                        verbResult = (true, naDirect.word);
                                    else
                                        verbResult = await DeconjugateVerbOrAdjective(wordData, deconjugator, diagnostics);
                                }

                                if (!verbResult.success || verbResult.word == null)
                                {
                                    // Interjections are frequently misclassified by Sudachi as proper-name nouns (e.g., おお…).
                                    // Try direct lookup as an interjection before giving up.
                                    wordData.wordInfo.PartOfSpeech = PartOfSpeech.Interjection;
                                    var interjectionDirect = await DeconjugateWord(wordData, diagnostics);
                                    if (interjectionDirect.success && interjectionDirect.word != null)
                                        verbResult = (true, interjectionDirect.word);
                                }

                                wordData.wordInfo.PartOfSpeech = oldPos;
                                processedWord = verbResult.word;
                            }
                            else if (wordData.wordInfo.PartOfSpeech is PartOfSpeech.Pronoun or PartOfSpeech.Conjunction or PartOfSpeech.Interjection)
                            {
                                processedWord = nounResult.word;
                            }
                            else
                            {
                                // Also try verb deconjugation: noun-tagged tokens may be verb stems
                                // (e.g., 抱え is both a rare noun "armful" and 連用形 of the common verb 抱える).
                                // Prefer the verb if it has strictly higher priority.
                                var savedPos = wordData.wordInfo.PartOfSpeech;
                                wordData.wordInfo.PartOfSpeech = PartOfSpeech.Verb;
                                var verbFallback = await DeconjugateVerbOrAdjective(wordData, deconjugator, diagnostics);
                                wordData.wordInfo.PartOfSpeech = savedPos;

                                if (verbFallback.success && verbFallback.word != null && nounResult.word != null)
                                {
                                    var bothCache = await JmDictCache.GetWordsAsync(
                                        [nounResult.word.WordId, verbFallback.word.WordId]);
                                    if (bothCache.TryGetValue(nounResult.word.WordId, out var nounEntry) &&
                                        bothCache.TryGetValue(verbFallback.word.WordId, out var verbEntry))
                                    {
                                        bool isKana = WanaKana.IsKana(wordData.wordInfo.Text);
                                        processedWord = verbEntry.GetPriorityScore(isKana) > nounEntry.GetPriorityScore(isKana)
                                            ? verbFallback.word
                                            : nounResult.word;
                                    }
                                    else
                                    {
                                        processedWord = nounResult.word;
                                    }
                                }
                                else
                                {
                                    processedWord = nounResult.word;
                                }
                            }
                        }

                        if (processedWord != null)
                        {
                            // Restore original text (before any stripping/modifications)
                            processedWord.OriginalText = baseWord;
                            break;
                        }

                        // We haven't found a match, let's try to remove the last character if it's a っ, a ー or a duplicate
                        if (wordData.wordInfo.Text.Length > 2 &&
                            (wordData.wordInfo.Text[^1] == 'っ' || wordData.wordInfo.Text[^1] == 'ー' ||
                             wordData.wordInfo.Text[^2] == wordData.wordInfo.Text[^1]))
                        {
                            wordData.wordInfo.Text = wordData.wordInfo.Text[..^1];
                        }
                        // Let's try to remove any honorifics in front of the word
                        else if (wordData.wordInfo.Text.StartsWith("お") || wordData.wordInfo.Text.StartsWith("御"))
                        {
                            wordData.wordInfo.Text = wordData.wordInfo.Text[1..];
                        }
                        // Let's try without any long vowel mark
                        else if (wordData.wordInfo.Text.Contains('ー'))
                        {
                            wordData.wordInfo.Text = wordData.wordInfo.Text.Replace("ー", "");
                        }
                        // Let's try without small っ
                        else if (wordData.wordInfo.Text.Contains('っ') || wordData.wordInfo.Text.Contains('ッ'))
                        {
                            wordData.wordInfo.Text = baseWord.Replace("っ", "").Replace("ッ", "");
                        }
                        // Let's try stripping any small kana
                        else if (wordData.wordInfo.Text.Contains('ゃ') || wordData.wordInfo.Text.Contains('ゅ') ||
                                 wordData.wordInfo.Text.Contains('ょ') || wordData.wordInfo.Text.Contains('ぁ') ||
                                 wordData.wordInfo.Text.Contains('ぃ') || wordData.wordInfo.Text.Contains('ぅ') ||
                                 wordData.wordInfo.Text.Contains('ぇ') || wordData.wordInfo.Text.Contains('ぉ'))
                        {
                            wordData.wordInfo.Text = baseWord.Replace("ゃ", "").Replace("ゅ", "")
                                                             .Replace("ょ", "").Replace("ぁ", "")
                                                             .Replace("ぃ", "").Replace("ぅ", "")
                                                             .Replace("ぇ", "").Replace("ぉ", "")
                                                             .Replace("っ", "").Replace("ッ", "");
                        }
                        else
                        {
                            isProcessed = true;
                        }

                        // Also stop if we've made too many attempts
                        if (attemptCount >= maxAttempts)
                        {
                            isProcessed = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log and consider this word processed to avoid infinite loop
                        Console.WriteLine($"[Error] Failed to process word '{wordData.wordInfo.Text}': {ex.Message}");
                        isProcessed = true;
                    }
                } while (!isProcessed);

                // Always restore original text to avoid mutating shared WordInfo
                // (used by example sentence extraction and diagnostics)
                wordData.wordInfo.Text = baseWord;

                if (processedWord == null)
                {
                    diagnostics?.RunSummary.IncrementUnresolvedTokenCount();
                    return ProcessWordResult.Unresolved;
                }

                processedWord.Occurrences = wordData.occurrences;
                processedWord.SudachiReading = wordData.wordInfo.Reading;

                if (!UseCache)
                    return ProcessWordResult.FromResolved(processedWord);

                try
                {
                    await DeckWordCache.SetAsync(cacheKey,
                                                 new DeckWord
                                                 {
                                                     WordId = processedWord.WordId, OriginalText = processedWord.OriginalText,
                                                     ReadingIndex = processedWord.ReadingIndex, Conjugations = processedWord.Conjugations,
                                                     PartsOfSpeech = processedWord.PartsOfSpeech, Origin = processedWord.Origin
                                                 }, CommandFlags.FireAndForget);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Warning] Failed to write to DeckWordCache: {ex.Message}");
                }


                return ProcessWordResult.FromResolved(processedWord);
            }
            finally
            {
                _processSemaphore.Release();
            }
        }

        private static async Task<(bool success, DeckWord? word)> DeconjugateWord((WordInfo wordInfo, int occurrences) wordData,
                                                                                  ParserDiagnostics? diagnostics = null)
        {
            string text = wordData.wordInfo.Text;

            // Exclude text that is primarily digits (with optional trailing ー) or single latin character
            var textWithoutBar = text.TrimEnd('ー');
            if ((textWithoutBar.Length > 0 && textWithoutBar.All(char.IsDigit)) || (text.Length == 1 && text.IsAsciiOrFullWidthLetter()))
            {
                return (false, null);
            }

            _lookups.TryGetValue(text, out List<int>? candidates);
            var textInHiragana = WanaKana.ToHiragana(wordData.wordInfo.Text, new DefaultOptions() { ConvertLongVowelMark = false, });
            _lookups.TryGetValue(textInHiragana, out var candidatesHiragana);

            candidates = candidates != null ? new List<int>(candidates) : [];

            var textNormalized = KanaNormalizer.Normalize(textInHiragana);
            if (textNormalized != textInHiragana)
            {
                _lookups.TryGetValue(textNormalized, out List<int>? candidatesNormalized);
                if (candidatesNormalized is { Count: not 0 })
                    candidates.AddRange(candidatesNormalized);
            }


            // Add candidatesInHiragana to candidates, deduplicate 
            if (candidatesHiragana is { Count: not 0 })
            {
                var newCandidates = new List<int>(candidates);
                newCandidates.AddRange(candidatesHiragana);
                candidates = newCandidates.Distinct().ToList();
            }

            bool isStripped = false;

            string textStripped = "";
            if (text.Contains('ー'))
            {
                isStripped = true;
                textStripped = text.Replace("ー", "");

                // HEURISTIC:
                // If the word ends with 'ー' and the stripped version is very short (<= 2 chars),
                // it is almost certainly a slang adjective (e.g., すげー -> すげ, やべー -> やべ).
                // We should SKIP the stripped lookup here to avoid matching random nouns like "Sedge" (すげ).
                //
                // However, allow it if the 'ー' was in the middle (e.g., だーかーら -> だから).
                bool endsInBar = text.EndsWith("ー");
                bool isShort = textStripped.Length <= 2;

                if (!endsInBar || !isShort)
                {
                    var textStrippedHira = WanaKana.ToHiragana(textStripped);
                    _lookups.TryGetValue(textStrippedHira, out List<int>? stripIds);
                    if (stripIds != null) candidates ??= new List<int>();
                    if (stripIds != null) candidates.AddRange(stripIds);
                }
            }

            if (candidates is { Count: not 0 })
            {
                candidates = candidates.OrderBy(c => c).ToList();

                Dictionary<int, JmDictWord> wordCache;
                try
                {
                    wordCache = await JmDictCache.GetWordsAsync(candidates);
                }
                catch (Exception ex)
                {
                    // If we hit an exception when retrieving from cache, return a failure
                    // but don't crash the entire process
                    Console.WriteLine($"Error retrieving word cache: {ex.Message}");
                    return (false, null);
                }

                // Early return if we got no words from cache to avoid NullReferenceException
                if (wordCache.Count == 0)
                {
                    return (false, null);
                }

                bool isNameLikeSudachiNoun = PosMapper.IsNameLikeSudachiNoun(
                                                                             wordData.wordInfo.PartOfSpeech,
                                                                             wordData.wordInfo.PartOfSpeechSection1,
                                                                             wordData.wordInfo.PartOfSpeechSection2,
                                                                             wordData.wordInfo.PartOfSpeechSection3);

                bool hasAnyNonNameCandidate = false;
                var compatibleNonNameMatches = new List<JmDictWord>();
                var nameCandidates = new List<JmDictWord>();

                foreach (var id in candidates)
                {
                    if (!wordCache.TryGetValue(id, out var word)) continue;

                    var posList = word.PartsOfSpeech.ToPartOfSpeech();
                    bool hasNonNamePos = posList.Any(p => p is not (PartOfSpeech.Name or PartOfSpeech.Unknown));
                    if (hasNonNamePos)
                        hasAnyNonNameCandidate = true;

                    // Treat pure-name entries (JMnedict name entries) separately from normal words.
                    // Some words may include both Name + non-name tags; those should be treated as non-name to avoid regressions.
                    bool isPureNameEntry = !hasNonNamePos && posList.Contains(PartOfSpeech.Name);

                    // Is stripped part to handle interjection like よー and こーら
                    bool compatible = PosMapper.IsJmDictCompatibleWithSudachi(
                                                                              word.PartsOfSpeech,
                                                                              wordData.wordInfo.PartOfSpeech,
                                                                              allowInterjectionFallback: isStripped);

                    if (compatible && hasNonNamePos)
                    {
                        compatibleNonNameMatches.Add(word);
                        continue;
                    }

                    // Names should be strongly deprioritized unless we have strong evidence.
                    // We allow them to be considered when:
                    // - the token is in a person-name honorific context (Xさん/Xくん/etc), OR
                    // - Sudachi itself classified the token as a proper/name-like noun.
                    if (isPureNameEntry && (wordData.wordInfo.IsPersonNameContext || isNameLikeSudachiNoun))
                    {
                        nameCandidates.Add(word);
                    }
                }

                // Selection via pair scoring:
                // Build (word, form) candidates from the appropriate pool, then pick the best pair.
                List<JmDictWord> candidatePool;
                bool isNameContext;

                if (wordData.wordInfo.IsPersonNameContext && nameCandidates.Count > 0)
                {
                    candidatePool = nameCandidates;
                    isNameContext = true;
                }
                else if (compatibleNonNameMatches.Count > 0)
                {
                    candidatePool = compatibleNonNameMatches;
                    isNameContext = false;
                }
                else if (!hasAnyNonNameCandidate && nameCandidates.Count > 0)
                {
                    candidatePool = nameCandidates;
                    isNameContext = true;
                }
                else
                {
                    return (false, null);
                }

                var allFormCandidates = new List<FormCandidate>();
                foreach (JmDictWord word in candidatePool)
                {
                    var forms = EnumerateCandidateForms(word, textInHiragana, allowLooseLvmMatch: true, surface: text);
                    allFormCandidates.AddRange(forms);

                    // Also try with stripped text if applicable
                    if (!isStripped)
                        continue;

                    var strippedHira = WanaKana.ToHiragana(textStripped, new DefaultOptions { ConvertLongVowelMark = false });
                    if (strippedHira == textInHiragana)
                        continue;

                    var strippedForms = EnumerateCandidateForms(word, strippedHira, allowLooseLvmMatch: true, surface: textStripped);
                    allFormCandidates.AddRange(strippedForms);
                }

                var bestPair = PickBestFormCandidate(allFormCandidates, text,
                                                     wordData.wordInfo.DictionaryForm, wordData.wordInfo.NormalizedForm, isNameContext,
                                                     diagnostics,
                                                     sudachiReading: wordData.wordInfo.Reading);

                if (bestPair == null)
                    return (false, null);

                DeckWord deckWord = new()
                                    {
                                        WordId = bestPair.Word.WordId, OriginalText = wordData.wordInfo.Text,
                                        ReadingIndex = bestPair.ReadingIndex, Occurrences = wordData.occurrences,
                                        PartsOfSpeech = bestPair.Word.PartsOfSpeech.ToPartOfSpeech(), Origin = bestPair.Word.Origin
                                    };
                return (true, deckWord);
            }

            return (false, null);
        }

        private static async Task<(bool success, DeckWord? word)> DeconjugateVerbOrAdjective(
            (WordInfo wordInfo, int occurrences) wordData, Deconjugator deconjugator,
            ParserDiagnostics? diagnostics = null)
        {
            // Early check for digits before WanaKana (which can't convert full-width digits)
            var textWithoutBar = wordData.wordInfo.Text.TrimEnd('ー');
            if (textWithoutBar.Length > 0 && textWithoutBar.All(char.IsDigit) ||
                (textWithoutBar.Length == 1 && textWithoutBar.IsAsciiOrFullWidthLetter()))
            {
                return (false, null);
            }

            var normalizedText = KanaNormalizer.Normalize(WanaKana.ToHiragana(wordData.wordInfo.Text));

            // Exclude single latin character
            if (normalizedText.Length == 1 && normalizedText.IsAsciiOrFullWidthLetter())
            {
                return (false, null);
            }

            var deconjugated = deconjugator.Deconjugate(normalizedText)
                                           .OrderByDescending(d => d.Text.Length).ToList();

            List<(DeconjugationForm form, List<int> ids)> candidates = new();
            foreach (var form in deconjugated)
            {
                if (_lookups.TryGetValue(form.Text, out List<int>? lookup))
                {
                    candidates.Add((form, lookup));
                }
            }

            // Track if we need to try DictionaryForm lookup later (for compound expressions)
            bool tryDictionaryFormFallback = candidates.Count == 0 && !string.IsNullOrEmpty(wordData.wordInfo.DictionaryForm);

            // if there's a candidate that's the same as the base word, put it first in the list
            var baseDictionaryWord = WanaKana.ToHiragana(wordData.wordInfo.DictionaryForm.Replace("ゎ", "わ").Replace("ヮ", "わ"),
                                                         new DefaultOptions() { ConvertLongVowelMark = false });
            var baseDictionaryWordIndex = candidates.FindIndex(c => c.form.Text == baseDictionaryWord);
            if (baseDictionaryWordIndex != -1)
            {
                var baseDictionaryWordCandidate = candidates[baseDictionaryWordIndex];
                candidates.RemoveAt(baseDictionaryWordIndex);
                candidates.Insert(0, baseDictionaryWordCandidate);
            }

            // if there's a candidate that's the same as the base word, put it first in the list
            var baseWord = WanaKana.ToHiragana(wordData.wordInfo.Text);
            var baseWordIndex = candidates.FindIndex(c => c.form.Text == baseWord);
            if (baseWordIndex != -1 && candidates[0].form.Text != baseDictionaryWord)
            {
                var baseWordCandidate = candidates[baseWordIndex];
                candidates.RemoveAt(baseWordIndex);
                candidates.Insert(0, baseWordCandidate);
            }

            var allCandidateIds = candidates.SelectMany(c => c.ids).Distinct().ToList();

            // If we have DictionaryForm fallback to try, add those IDs to the list
            if (tryDictionaryFormFallback)
            {
                if (_lookups.TryGetValue(baseDictionaryWord, out List<int>? dictFormLookupIds) ||
                    _lookups.TryGetValue(wordData.wordInfo.DictionaryForm, out dictFormLookupIds))
                {
                    if (dictFormLookupIds is { Count: > 0 })
                    {
                        allCandidateIds = allCandidateIds.Concat(dictFormLookupIds).Distinct().ToList();
                    }
                }

                // Also try NormalizedForm (e.g., 多き has DictionaryForm=多し but NormalizedForm=多い)
                if (dictFormLookupIds is not { Count: > 0 } && !string.IsNullOrEmpty(wordData.wordInfo.NormalizedForm))
                {
                    var normalizedHiragana = WanaKana.ToHiragana(wordData.wordInfo.NormalizedForm,
                                                                 new DefaultOptions() { ConvertLongVowelMark = false });
                    if (_lookups.TryGetValue(normalizedHiragana, out dictFormLookupIds) ||
                        _lookups.TryGetValue(wordData.wordInfo.NormalizedForm, out dictFormLookupIds))
                    {
                        if (dictFormLookupIds is { Count: > 0 })
                        {
                            allCandidateIds = allCandidateIds.Concat(dictFormLookupIds).Distinct().ToList();
                        }
                    }
                }
            }

            if (!allCandidateIds.Any())
                return (false, null);

            Dictionary<int, JmDictWord> wordCache;
            try
            {
                wordCache = await JmDictCache.GetWordsAsync(allCandidateIds);

                // Check if we got any results
                if (wordCache.Count == 0)
                {
                    return (false, null);
                }
            }
            catch (Exception ex)
            {
                // If we hit an exception when retrieving from cache, return a failure
                // but don't crash the entire process
                Console.WriteLine($"Error retrieving verb/adjective word cache: {ex.Message}");
                return (false, null);
            }

            List<(JmDictWord word, DeconjugationForm form)> matches = new();

            foreach (var candidate in candidates)
            {
                foreach (var id in candidate.ids)
                {
                    if (!wordCache.TryGetValue(id, out var word)) continue;

                    if (!PosMapper.IsJmDictCompatibleWithSudachi(word.PartsOfSpeech, wordData.wordInfo.PartOfSpeech))
                        continue;

                    // Validate that deconjugation POS tags match the word's POS.
                    // This prevents nouns from being incorrectly matched via verb deconjugation rules.
                    var formPosTags = PosMapper.GetValidatableDeconjTags(candidate.form.Tags).ToList();
                    if (formPosTags.Count > 0 && !PosMapper.AreDeconjTagsCompatibleWithJmDict(formPosTags, word.PartsOfSpeech))
                        continue;

                    matches.Add((word, candidate.form));
                }
            }

            // When Sudachi's DictionaryForm identifies a different base form (e.g., いかん → DictForm いく),
            // remove identity matches for suru-nouns that are only Verb-compatible through vs tags.
            // This prevents nouns like 移管 from winning via surface match over the actual expression (行かん)
            // or deconjugated verb (行く) when the surface happens to be homophonic with the suru-noun.
            if (matches.Count > 1 && normalizedText != baseDictionaryWord && !string.IsNullOrEmpty(baseDictionaryWord))
            {
                bool hasDictFormMatch = matches.Any(m => m.form.Text == baseDictionaryWord && m.form.Process.Count > 0);
                if (hasDictFormMatch)
                {
                    matches.RemoveAll(m =>
                        m.form.Process.Count == 0 &&
                        IsSuruNounWithoutExpression(m.word.PartsOfSpeech));
                }
            }

            if (matches.Count == 0)
            {
                // Last resort: try using Sudachi's DictionaryForm directly if available
                // This handles cases like おかけして → 掛ける where the お prefix
                // prevents standard deconjugation rules from matching
                if (!string.IsNullOrEmpty(wordData.wordInfo.DictionaryForm))
                {
                    var dictFormHiragana = WanaKana.ToHiragana(wordData.wordInfo.DictionaryForm.Replace("ゎ", "わ").Replace("ヮ", "わ"),
                                                               new DefaultOptions() { ConvertLongVowelMark = false });
                    if (_lookups.TryGetValue(dictFormHiragana, out List<int>? dictLookup) ||
                        _lookups.TryGetValue(wordData.wordInfo.DictionaryForm, out dictLookup))
                    {
                        if (dictLookup is { Count: > 0 })
                        {
                            try
                            {
                                var dictWordCache = await JmDictCache.GetWordsAsync(dictLookup);
                                var recoveredProcess = deconjugated
                                                       .Where(d => d.Process.Count > 0 && d.Text.StartsWith(dictFormHiragana))
                                                       .MinBy(d => d.Text.Length)?.Process
                                                       ?.Where(p => !string.IsNullOrEmpty(p)).ToList() ?? [];
                                foreach (var dictWord in dictWordCache.Values)
                                {
                                    List<PartOfSpeech> pos = dictWord.PartsOfSpeech.ToPartOfSpeech();
                                    if (pos.Contains(wordData.wordInfo.PartOfSpeech))
                                    {
                                        var form = new DeconjugationForm(dictFormHiragana, wordData.wordInfo.Text,
                                                                         new List<string>(), new HashSet<string>(), recoveredProcess);
                                        matches.Add((dictWord, form));
                                    }
                                }
                            }
                            catch
                            {
                                /* Cache lookup failed */
                            }
                        }
                    }
                }

                // Also try NormalizedForm (e.g., 多き has DictionaryForm=多し but NormalizedForm=多い)
                if (matches.Count == 0 && !string.IsNullOrEmpty(wordData.wordInfo.NormalizedForm) &&
                    wordData.wordInfo.NormalizedForm != wordData.wordInfo.DictionaryForm)
                {
                    var normalizedHiragana = WanaKana.ToHiragana(wordData.wordInfo.NormalizedForm,
                                                                 new DefaultOptions() { ConvertLongVowelMark = false });
                    if (_lookups.TryGetValue(normalizedHiragana, out List<int>? normalizedLookup) ||
                        _lookups.TryGetValue(wordData.wordInfo.NormalizedForm, out normalizedLookup))
                    {
                        if (normalizedLookup is { Count: > 0 })
                        {
                            try
                            {
                                var normalizedWordCache = await JmDictCache.GetWordsAsync(normalizedLookup);
                                var recoveredProcess = deconjugated
                                                       .Where(d => d.Process.Count > 0 &&
                                                                    (d.Text.StartsWith(normalizedHiragana) || d.Text.StartsWith(baseDictionaryWord)))
                                                       .MinBy(d => d.Text.Length)?.Process
                                                       ?.Where(p => !string.IsNullOrEmpty(p)).ToList() ?? [];
                                foreach (var normalizedWord in normalizedWordCache.Values)
                                {
                                    List<PartOfSpeech> pos = normalizedWord.PartsOfSpeech.ToPartOfSpeech();
                                    if (pos.Contains(wordData.wordInfo.PartOfSpeech))
                                    {
                                        var form = new DeconjugationForm(normalizedHiragana, wordData.wordInfo.Text,
                                                                         new List<string>(), new HashSet<string>(), recoveredProcess);
                                        matches.Add((normalizedWord, form));
                                    }
                                }
                            }
                            catch
                            {
                                /* Cache lookup failed */
                            }
                        }
                    }
                }

                if (matches.Count == 0)
                {
                    return (false, null);
                }
            }

            // Build (word, jmDictForm, deconjForm) triples across all matches and pick the best pair by score.
            // Each match's deconjForm.Text serves as the targetHiragana for phonetic gating.
            var allFormCandidates = new List<FormCandidate>();
            foreach (var match in matches)
            {
                var formCandidates = EnumerateCandidateForms(match.word, match.form.Text,
                                                             allowLooseLvmMatch: true, deconjForm: match.form,
                                                             surface: wordData.wordInfo.Text);
                allFormCandidates.AddRange(formCandidates);
            }

            var bestPair = PickBestFormCandidate(allFormCandidates, wordData.wordInfo.Text,
                                                 wordData.wordInfo.DictionaryForm, wordData.wordInfo.NormalizedForm, isNameContext: false,
                                                 diagnostics,
                                                 sudachiReading: wordData.wordInfo.Reading);

            // Imperative disambiguation: godan imperative (行けよ→行く "go!") vs potential imperative
            // (行けよ→行ける "be able to go!"). The base verb is almost always correct in natural Japanese.
            if (bestPair != null
                && wordData.wordInfo.IsImperative
                && !string.IsNullOrEmpty(wordData.wordInfo.NormalizedForm)
                && wordData.wordInfo.NormalizedForm != wordData.wordInfo.DictionaryForm)
            {
                var normalizedHira = WanaKana.ToHiragana(wordData.wordInfo.NormalizedForm,
                                                          new DefaultOptions { ConvertLongVowelMark = false });
                var normalizedFormHira = KanaNormalizer.Normalize(normalizedHira);

                var baseVerbCandidate = allFormCandidates
                    .Where(c => c.Form.Text == wordData.wordInfo.NormalizedForm
                                || KanaNormalizer.Normalize(
                                    WanaKana.ToHiragana(c.Form.Text,
                                                        new DefaultOptions { ConvertLongVowelMark = false })) == normalizedFormHira)
                    .OrderByDescending(c => c.TotalScore)
                    .FirstOrDefault();

                if (baseVerbCandidate != null)
                    bestPair = baseVerbCandidate;
            }

            if (bestPair == null)
                return (false, null);

            DeckWord deckWord = new()
                                {
                                    WordId = bestPair.Word.WordId, OriginalText = wordData.wordInfo.Text,
                                    ReadingIndex = bestPair.ReadingIndex, Occurrences = wordData.occurrences,
                                    Conjugations = bestPair.DeconjForm?.Process.ToList() ?? [],
                                    PartsOfSpeech = bestPair.Word.PartsOfSpeech.ToPartOfSpeech(), Origin = bestPair.Word.Origin
                                };

            return (true, deckWord);
        }

        public static async Task<List<DeckWord>> GetWordsDirectLookup(IDbContextFactory<JitenDbContext> contextFactory, List<string> words)
        {
            await EnsureInitializedAsync(contextFactory);

            List<DeckWord> matchedWords = new();

            foreach (var word in words)
            {
                var wordInHiragana = WanaKana.ToHiragana(word, new DefaultOptions() { ConvertLongVowelMark = false, });
                var wordNormalized = KanaNormalizer.Normalize(wordInHiragana);

                if (!_lookups.TryGetValue(wordNormalized, out var matchesIds) || matchesIds.Count == 0)
                    continue;

                var wordCache = await JmDictCache.GetWordsAsync(matchesIds);

                if (wordCache == null || wordCache.Count == 0)
                    continue;

                List<(JmDictWord match, int readingIndex)> matchesWithReading = new();

                foreach (var id in matchesIds)
                {
                    if (!wordCache.TryGetValue(id, out var match)) continue;
                    var matchedForm = match.Forms.FirstOrDefault(f => f.Text == word);
                    if (matchedForm != null)
                        matchesWithReading.Add((match, matchedForm.ReadingIndex));
                }

                if (matchesWithReading.Count == 0)
                    continue;

                // Fetch frequency data from database
                var candidateWordIds = matchesWithReading.Select(m => m.match.WordId).ToList();
                await using var context = await _contextFactory.CreateDbContextAsync();
                var formFrequencies = await context.WordFormFrequencies
                                                   .AsNoTracking()
                                                   .Where(wff => candidateWordIds.Contains(wff.WordId))
                                                   .ToDictionaryAsync(wff => (wff.WordId, wff.ReadingIndex));

                // Order by frequency rank (lower = more frequent = better)
                var best = matchesWithReading
                           .OrderBy(m =>
                           {
                               var key = (m.match.WordId, (short)m.readingIndex);
                               if (formFrequencies.TryGetValue(key, out var wff))
                                   return wff.FrequencyRank;
                               return int.MaxValue;
                           })
                           .First();

                matchedWords.Add(new DeckWord
                                 {
                                     WordId = best.match.WordId, ReadingIndex = (byte)best.readingIndex, OriginalText = word,
                                     SudachiReading = GetKatakanaReading(best.match, (byte)best.readingIndex)
                                 });
            }

            return ExcludeFinalMisparses(matchedWords);
        }

        private static async Task<List<List<DeckWord>>> RescueFailedWords(
            List<(WordInfo wordInfo, int occurrences)> failedWords,
            Deconjugator deconjugator)
        {
            var groupedResults = failedWords.Select(_ => new List<DeckWord>()).ToList();

            for (int i = 0; i < failedWords.Count; i++)
            {
                var (wordInfo, occurrences) = failedWords[i];
                var text = wordInfo.Text;

                // Skip punctuation and digit only (including digits with trailing ー)
                var textWithoutBar = text.TrimEnd('ー');
                if (string.IsNullOrEmpty(text) ||
                    text.All(c => !char.IsLetterOrDigit(c)) ||
                    (textWithoutBar.Length > 0 && textWithoutBar.All(char.IsDigit) ||
                     (textWithoutBar.Length == 1 && textWithoutBar.IsAsciiOrFullWidthLetter())))
                    continue;

                // Check cache for already-attempted rescues
                if (UseCache)
                {
                    try
                    {
                        var isNameLikeSudachiNoun = PosMapper.IsNameLikeSudachiNoun(wordInfo.PartOfSpeech, wordInfo.PartOfSpeechSection1,
                                                                                    wordInfo.PartOfSpeechSection2,
                                                                                    wordInfo.PartOfSpeechSection3);
                        var cacheKey = new DeckWordCacheKey(text, wordInfo.PartOfSpeech, wordInfo.DictionaryForm,
                                                            wordInfo.Reading, wordInfo.IsPersonNameContext,
                                                            isNameLikeSudachiNoun);
                        var cached = await DeckWordCache.GetAsync(cacheKey);
                        if (cached is { WordId: -1 })
                            continue;
                    }
                    catch
                    {
                        /* Cache read failed, proceed */
                    }
                }

                var results = new List<DeckWord>();

                while (text.Length > 0)
                {
                    DeckWord? match = null;
                    int matchLen = 0;

                    // Try window sizes from largest to smallest
                    for (int windowSize = Math.Min(10, text.Length); windowSize >= 1; windowSize--)
                    {
                        var candidate = text[..windowSize];

                        // Skip candidates that are pure digits
                        var candidateWithoutBar = candidate.TrimEnd('ー');
                        if (candidateWithoutBar.Length > 0 && candidateWithoutBar.All(char.IsDigit) ||
                            (candidateWithoutBar.Length == 1 && candidateWithoutBar.IsAsciiOrFullWidthLetter()))
                            continue;

                        string hiragana;
                        try
                        {
                            hiragana = WanaKana.ToHiragana(candidate, new DefaultOptions { ConvertLongVowelMark = false });
                        }
                        catch (KeyNotFoundException)
                        {
                            continue; // Skip this window size if conversion fails (e.g., full-width digits)
                        }

                        var normalized = KanaNormalizer.Normalize(hiragana);

                        // Try direct lookup
                        List<int>? wordIds = null;
                        if (_lookups.TryGetValue(candidate, out wordIds) ||
                            _lookups.TryGetValue(hiragana, out wordIds) ||
                            _lookups.TryGetValue(normalized, out wordIds))
                        {
                            if (wordIds is { Count: > 0 })
                            {
                                try
                                {
                                    var wordCache = await JmDictCache.GetWordsAsync(wordIds);
                                    if (wordCache.Count > 0)
                                    {
                                        // For single-char honorific prefixes (ご, お), prefer Prefix over Name
                                        var candidates = wordCache.Values.AsEnumerable();
                                        if (candidate is "ご" or "お")
                                        {
                                            var prefixMatch =
                                                candidates.FirstOrDefault(w => w.PartsOfSpeech.ToPartOfSpeech()
                                                                                .Contains(PartOfSpeech.Prefix));
                                            if (prefixMatch != null)
                                                candidates = [prefixMatch];
                                        }

                                        // Deprioritise pure-name entries (JMnedict) so common nouns win
                                        var nonNameCandidates = candidates
                                                                .Where(w => w.PartsOfSpeech.ToPartOfSpeech()
                                                                             .Any(p => p is not (PartOfSpeech.Name
                                                                                      or PartOfSpeech.Unknown)))
                                                                .ToList();
                                        if (nonNameCandidates.Count > 0)
                                            candidates = nonNameCandidates;

                                        var bestMatch = candidates
                                                        .OrderByDescending(w => w.GetPriorityScore(WanaKana.IsKana(candidate)))
                                                        .First();

                                        // Check for compound verb context: if match is a noun and remainder
                                        // starts with a verb, check if candidate could be a verb stem instead
                                        var remainder = text[windowSize..];
                                        var bestPos = bestMatch.PartsOfSpeech.ToPartOfSpeech();
                                        if (remainder.Length > 0 &&
                                            bestPos.Contains(PartOfSpeech.Noun) &&
                                            !bestPos.Any(p => p is PartOfSpeech.Verb or PartOfSpeech.IAdjective))
                                        {
                                            // Check if there's a verb whose stem matches this candidate
                                            // Try common verb endings: す→し, く→き, ぐ→ぎ, む→み, ぶ→び, ぬ→に, う→い, つ→ち, る→り
                                            var candidateHira =
                                                WanaKana.ToHiragana(candidate, new DefaultOptions { ConvertLongVowelMark = false });
                                            string? verbForm = null;
                                            if (candidateHira.EndsWith("し")) verbForm = candidateHira[..^1] + "す";
                                            else if (candidateHira.EndsWith("き")) verbForm = candidateHira[..^1] + "く";
                                            else if (candidateHira.EndsWith("ぎ")) verbForm = candidateHira[..^1] + "ぐ";
                                            else if (candidateHira.EndsWith("み")) verbForm = candidateHira[..^1] + "む";
                                            else if (candidateHira.EndsWith("び")) verbForm = candidateHira[..^1] + "ぶ";
                                            else if (candidateHira.EndsWith("に")) verbForm = candidateHira[..^1] + "ぬ";
                                            else if (candidateHira.EndsWith("い")) verbForm = candidateHira[..^1] + "う";
                                            else if (candidateHira.EndsWith("ち")) verbForm = candidateHira[..^1] + "つ";
                                            else if (candidateHira.EndsWith("り")) verbForm = candidateHira[..^1] + "る";

                                            if (verbForm != null && _lookups.TryGetValue(verbForm, out var verbWordIds))
                                            {
                                                var verbCache = await JmDictCache.GetWordsAsync(verbWordIds);
                                                var verbMatch = verbCache.Values.FirstOrDefault(w =>
                                                    w.PartsOfSpeech.ToPartOfSpeech().Any(p => p == PartOfSpeech.Verb));
                                                if (verbMatch != null)
                                                {
                                                    // Found a verb - prefer this interpretation over the noun
                                                    var verbReadingIndex = GetBestReadingIndex(verbMatch, verbForm);
                                                    if (verbReadingIndex != 255)
                                                    {
                                                        match = new DeckWord
                                                                {
                                                                    WordId = verbMatch.WordId, OriginalText = candidate,
                                                                    ReadingIndex = verbReadingIndex, Occurrences = occurrences,
                                                                    Conjugations = ["(masu stem)"],
                                                                    PartsOfSpeech = verbMatch.PartsOfSpeech.ToPartOfSpeech(),
                                                                    Origin = verbMatch.Origin,
                                                                    SudachiReading = GetKatakanaReading(verbMatch, verbReadingIndex)
                                                                };
                                                        matchLen = windowSize;
                                                        break;
                                                    }
                                                }
                                            }
                                        }

                                        var readingIndex = GetBestReadingIndex(bestMatch, candidate);
                                        // Skip single-char matches that are primarily numerals
                                        if (readingIndex != 255 && !(candidate.Length == 1 && bestMatch.PartsOfSpeech.Contains("num")))
                                        {
                                            match = new DeckWord
                                                    {
                                                        WordId = bestMatch.WordId, OriginalText = candidate, ReadingIndex = readingIndex,
                                                        Occurrences = occurrences, PartsOfSpeech = bestMatch.PartsOfSpeech.ToPartOfSpeech(),
                                                        Origin = bestMatch.Origin,
                                                        SudachiReading = GetKatakanaReading(bestMatch, readingIndex)
                                                    };
                                            matchLen = windowSize;
                                            break;
                                        }
                                    }
                                }
                                catch
                                {
                                }
                            }
                        }

                        // Try deconjugation
                        var deconjugated = deconjugator.Deconjugate(hiragana);
                        foreach (var form in deconjugated.OrderByDescending(f => f.Text.Length))
                        {
                            if (_lookups.TryGetValue(form.Text, out wordIds) && wordIds is { Count: > 0 })
                            {
                                try
                                {
                                    var wordCache = await JmDictCache.GetWordsAsync(wordIds);
                                    if (wordCache.Count > 0)
                                    {
                                        // For single-char honorific prefixes (ご, お), prefer Prefix over Name
                                        var deconjCandidates = wordCache.Values.AsEnumerable();
                                        if (candidate is "ご" or "お")
                                        {
                                            var prefixMatch =
                                                deconjCandidates.FirstOrDefault(w => w.PartsOfSpeech.ToPartOfSpeech()
                                                                                      .Contains(PartOfSpeech.Prefix));
                                            if (prefixMatch != null)
                                                deconjCandidates = [prefixMatch];
                                        }

                                        // Deprioritise pure-name entries (JMnedict) so common words win
                                        var nonNameDeconj = deconjCandidates
                                                            .Where(w => w.PartsOfSpeech.ToPartOfSpeech()
                                                                         .Any(p => p is not (PartOfSpeech.Name or PartOfSpeech.Unknown)))
                                                            .ToList();
                                        if (nonNameDeconj.Count > 0)
                                            deconjCandidates = nonNameDeconj;

                                        // Filter candidates by POS compatibility with deconjugation tags
                                        var formPosTags = PosMapper.GetValidatableDeconjTags(form.Tags).ToList();
                                        if (formPosTags.Count > 0)
                                        {
                                            deconjCandidates =
                                                deconjCandidates.Where(w =>
                                                                           PosMapper.AreDeconjTagsCompatibleWithJmDict(formPosTags,
                                                                               w.PartsOfSpeech));
                                        }

                                        var bestMatch = deconjCandidates
                                                        .OrderByDescending(w => w.GetPriorityScore(WanaKana.IsKana(candidate)))
                                                        .FirstOrDefault();
                                        if (bestMatch == null) continue;

                                        var readingIndex = GetBestReadingIndex(bestMatch, form.Text);
                                        // Skip single-char matches that are primarily numerals
                                        if (readingIndex != 255 && !(candidate.Length == 1 && bestMatch.PartsOfSpeech.Contains("num")))
                                        {
                                            match = new DeckWord
                                                    {
                                                        WordId = bestMatch.WordId, OriginalText = candidate, ReadingIndex = readingIndex,
                                                        Occurrences = occurrences, Conjugations = form.Process.ToList(),
                                                        PartsOfSpeech = bestMatch.PartsOfSpeech.ToPartOfSpeech(), Origin = bestMatch.Origin,
                                                        SudachiReading = GetKatakanaReading(bestMatch, readingIndex)
                                                    };
                                            matchLen = windowSize;
                                            break;
                                        }
                                    }
                                }
                                catch
                                {
                                }
                            }
                        }

                        if (match != null) break;
                    }

                    if (match != null)
                    {
                        results.Add(match);
                        text = text[matchLen..];

                        // Skip trailing ー if the match already ends with ー (emphasis marks)
                        if (match.OriginalText.EndsWith("ー"))
                        {
                            while (text.Length > 0 && text[0] == 'ー')
                                text = text[1..];
                        }
                    }
                    else
                    {
                        // No match found, skip one character and continue
                        text = text.Length > 1 ? text[1..] : "";
                    }
                }

                // Cache failed rescue attempts
                if (UseCache && results.Count == 0)
                {
                    try
                    {
                        var isNameLikeSudachiNoun = PosMapper.IsNameLikeSudachiNoun(wordInfo.PartOfSpeech, wordInfo.PartOfSpeechSection1,
                                                                                    wordInfo.PartOfSpeechSection2,
                                                                                    wordInfo.PartOfSpeechSection3);
                        var cacheKey = new DeckWordCacheKey(wordInfo.Text, wordInfo.PartOfSpeech, wordInfo.DictionaryForm,
                                                            wordInfo.Reading, wordInfo.IsPersonNameContext, isNameLikeSudachiNoun);
                        await DeckWordCache.SetAsync(cacheKey,
                                                     new DeckWord { WordId = -1, OriginalText = wordInfo.Text },
                                                     CommandFlags.FireAndForget);
                    }
                    catch
                    {
                    }
                }

                groupedResults[i] = results;
            }

            return groupedResults;
        }

        private static string GetKatakanaReading(JmDictWord word, byte readingIndex)
        {
            var kanaForm = word.Forms.FirstOrDefault(f => f.FormType == JmDictFormType.KanaForm && f.ReadingIndex == readingIndex);
            return kanaForm != null ? WanaKana.ToKatakana(kanaForm.Text, new DefaultOptions { ConvertLongVowelMark = false }) : "";
        }

        private static byte GetBestReadingIndex(JmDictWord word, string originalText)
        {
            if (word.Forms.Count == 0)
                return 0;

            var targetHiragana = WanaKana.ToHiragana(originalText, new DefaultOptions { ConvertLongVowelMark = false });
            var candidates = EnumerateCandidateForms(word, targetHiragana, allowLooseLvmMatch: true, surface: originalText);

            if (candidates.Count == 0)
                return 255;

            var best = PickBestFormCandidate(candidates, originalText,
                                             dictionaryForm: null, normalizedForm: null, isNameContext: false);

            return best?.ReadingIndex ?? 255;
        }

        private static async Task CombineCompounds(List<SentenceInfo> sentences)
        {
            foreach (var sentence in sentences)
            {
                if (sentence.Words.Count < 2)
                    continue;

                var wordInfos = sentence.Words.Select(w => w.word).ToList();
                var result = new List<(WordInfo word, int position, int length)>(sentence.Words.Count);

                // Right-to-left so verbs/adjectives at the end of compound expressions
                // get first pick of the full backward window, preventing smaller
                // sub-compounds from blocking larger ones (e.g. ことなきを得た)
                for (int i = wordInfos.Count - 1; i >= 0; i--)
                {
                    var word = wordInfos[i];

                    if (word.PartOfSpeech is PartOfSpeech.Verb or PartOfSpeech.IAdjective or PartOfSpeech.Expression or PartOfSpeech.Suffix)
                    {
                        var match = await TryMatchCompounds(wordInfos, i);
                        if (match.HasValue)
                        {
                            var (startIndex, dictForm, wordId) = match.Value;

                            var startPosition = sentence.Words[startIndex].position;
                            int combinedLength = 0;
                            for (int j = startIndex; j <= i; j++)
                            {
                                combinedLength += sentence.Words[j].length;
                            }

                            var originalText = string.Concat(wordInfos.Skip(startIndex).Take(i - startIndex + 1).Select(w => w.Text));
                            var combinedReading = string.Concat(wordInfos.Skip(startIndex).Take(i - startIndex + 1).Select(w => w.Reading));
                            var combinedWordInfo = new WordInfo
                                                   {
                                                       Text = originalText, DictionaryForm = dictForm,
                                                       PartOfSpeech = PartOfSpeech.Expression, NormalizedForm = dictForm,
                                                       Reading = WanaKana.ToHiragana(combinedReading), PreMatchedWordId = wordId
                                                   };

                            result.Add((combinedWordInfo, startPosition, combinedLength));
                            i = startIndex;
                            continue;
                        }
                    }

                    result.Add(sentence.Words[i]);
                }

                result.Reverse();
                sentence.Words = result;
            }
        }

        /// <summary>
        /// Repairs Sudachi over-segmentation caused by trailing ー on non-standalone tokens.
        /// When Sudachi sees e.g. あなたー, it may split into あ + な + たー.
        /// This method scans for tokens ending with ー (that aren't standalone ー or purely katakana),
        /// tries merging backwards with a bounded window, and validates against JMDict lookups.
        /// If lookup matches WITH ー (e.g. すげー is its own entry), surface keeps ー.
        /// If lookup only matches WITHOUT ー (e.g. あなた for あなたー), surface strips ー.
        /// </summary>
        private static void RepairLongVowelMisparses(List<SentenceInfo> sentences)
        {
            foreach (var sentence in sentences)
            {
                for (int i = sentence.Words.Count - 1; i >= 0; i--)
                {
                    var word = sentence.Words[i].word;

                    // Only target tokens that end with ー but aren't a standalone ー symbol
                    if (!word.Text.EndsWith("ー") || word.Text == "ー")
                        continue;

                    // Skip long katakana tokens (コーヒー, パーティー, etc.) — Sudachi handles these well.
                    // Allow short katakana stems through (ヤバー, スゴー) as they may be slang adj-i.
                    var withoutBar = word.Text.Replace("ー", "");
                    if (withoutBar.Length > 2 && WanaKana.IsKatakana(withoutBar))
                        continue;

                    // Try merging backwards: longest match first (max 5 preceding tokens + current = 6 tokens)
                    int maxBackward = Math.Min(5, i);
                    bool merged = false;

                    for (int k = maxBackward; k >= 1; k--)
                    {
                        var candidateParts = sentence.Words
                                                     .Skip(i - k).Take(k + 1)
                                                     .Select(w => w.word.Text);
                        var candidateSurface = string.Concat(candidateParts);
                        candidateSurface = Regex.Replace(candidateSurface, "ー{2,}", "ー");

                        // Try candidateSurface WITH ー — skip KanaNormalizer for merged tokens
                        // since normalizing ー to a vowel on artificial merges causes false positives
                        if (TryLongVowelLookup(candidateSurface, useKanaNormalizer: false))
                        {
                            MergeLongVowelTokens(sentence, i - k, i, candidateSurface);
                            i = i - k;
                            merged = true;
                            break;
                        }

                        // Try barless key — strip ー from surface since the word doesn't contain it
                        var candidateKey = candidateSurface.TrimEnd('ー');
                        if (candidateKey.Length > 0 && TryLongVowelLookup(candidateKey))
                        {
                            MergeLongVowelTokens(sentence, i - k, i, candidateKey);
                            i = i - k;
                            merged = true;
                            break;
                        }

                        // Try deconjugation of barless key
                        if (candidateKey.Length > 0 && TryDeconjugatedLongVowelLookup(candidateKey))
                        {
                            MergeLongVowelTokens(sentence, i - k, i, candidateKey);
                            i = i - k;
                            merged = true;
                            break;
                        }
                    }

                    // No backward merge worked — check the current token itself:
                    // if WITH ー is a valid word (すげー, やべー), leave it;
                    // if only the barless form is valid, strip ー from the surface
                    if (merged)
                        continue;

                    var singleSurface = word.Text;

                    // If the surface has a direct (non-normalized) lookup, leave it alone.
                    // Preserves valid colloquial forms like すげー, やべー.
                    if (TryLongVowelLookup(singleSurface, useKanaNormalizer: false))
                        continue;

                    var singleKey = singleSurface.TrimEnd('ー');

                    // For short stems (ヤバー, スゴー), try adj-i resolution before normalized lookups
                    // to avoid matching name entries like すごう (Sugou) instead of すごい
                    if (singleKey.Length == 2 && TryResolveAdjStem(word, singleKey))
                        continue;

                    if (TryLongVowelLookup(singleSurface))
                        continue;

                    if (singleKey.Length > 0 && (TryLongVowelLookup(singleKey) || TryDeconjugatedLongVowelLookup(singleKey)))
                        word.Text = singleKey;
                }
            }
        }

        private static bool HasLookupForCompound(string dictForm)
        {
            if (_lookups == null) return true;
            if (_lookups.TryGetValue(dictForm, out var ids) && ids.Count > 0)
                return true;

            string hira;
            try
            {
                hira = KanaNormalizer.Normalize(WanaKana.ToHiragana(dictForm));
            }
            catch
            {
                return false;
            }

            return hira != dictForm && _lookups.TryGetValue(hira, out ids) && ids.Count > 0;
        }

        private static bool TryLongVowelLookup(string text, bool useKanaNormalizer = true)
        {
            if (_lookups.TryGetValue(text, out var ids) && ids.Count > 0)
                return true;

            string hira;
            try
            {
                hira = WanaKana.ToHiragana(text, new DefaultOptions { ConvertLongVowelMark = false });
            }
            catch
            {
                return false;
            }

            if (hira != text && _lookups.TryGetValue(hira, out ids) && ids.Count > 0)
                return true;

            if (!useKanaNormalizer)
                return false;

            var normalized = KanaNormalizer.Normalize(hira);
            return normalized != hira && _lookups.TryGetValue(normalized, out ids) && ids.Count > 0;
        }

        /// <summary>
        /// Resolves a short stem as an i-adjective by checking if stem+い exists in lookups.
        /// Used for slang elongation patterns: ヤバー→ヤバい (やばい), スゴー→スゴい (すごい).
        /// </summary>
        private static bool TryResolveAdjStem(WordInfo word, string stem)
        {
            string hira;
            try
            {
                hira = KanaNormalizer.Normalize(
                    WanaKana.ToHiragana(stem, new DefaultOptions { ConvertLongVowelMark = false }));
            }
            catch
            {
                return false;
            }

            foreach (var form in Deconjugator.Instance.Deconjugate(hira))
            {
                if (form.Tags.Any(t => t == "adj-i") && form.Text == hira + "い" && TryLongVowelLookup(form.Text))
                {
                    word.Text = stem + "い";
                    word.DictionaryForm = stem + "い";
                    word.PartOfSpeech = PartOfSpeech.IAdjective;
                    return true;
                }
            }

            return false;
        }

        private static bool TryDeconjugatedLongVowelLookup(string candidateKey)
        {
            string hira;
            try
            {
                hira = KanaNormalizer.Normalize(
                                                WanaKana.ToHiragana(candidateKey, new DefaultOptions { ConvertLongVowelMark = false }));
            }
            catch
            {
                return false;
            }

            foreach (var form in Deconjugator.Instance.Deconjugate(hira))
                if (TryLongVowelLookup(form.Text))
                    return true;

            return false;
        }

        private static void MergeLongVowelTokens(SentenceInfo sentence, int startIdx, int endIdx, string surface)
        {
            var firstToken = sentence.Words[startIdx];
            firstToken.word.Text = surface;

            int combinedLength = 0;
            for (int j = startIdx; j <= endIdx; j++)
                combinedLength += sentence.Words[j].length;

            sentence.Words[startIdx] = (firstToken.word, firstToken.position, combinedLength);

            int removeCount = endIdx - startIdx;
            if (removeCount > 0)
                sentence.Words.RemoveRange(startIdx + 1, removeCount);
        }

        /// <summary>
        /// Repairs tokens that were broken by trailing ー.
        /// When Sudachi sees e.g. 久しぶりー, it may split incorrectly.
        /// This method detects standalone ー tokens and tries to combine
        /// preceding tokens, validating against JMDict.
        ///
        /// Priority:
        /// 1. Try WITH ー (わー, ねー, すげー are valid words)
        /// 2. Try WITHOUT ー (久しぶりー → 久しぶり)
        /// 3. Fallback: attach ー to preceding token (preserve stylistic usage)
        /// </summary>
        private static void RepairLongVowelTokens(List<SentenceInfo> sentences)
        {
            foreach (var sentence in sentences)
            {
                for (int i = sentence.Words.Count - 1; i >= 0; i--)
                {
                    var word = sentence.Words[i].word;

                    // Find standalone ー tokens
                    if (word.Text != "ー" || word.PartOfSpeech != PartOfSpeech.SupplementarySymbol)
                        continue;

                    if (i == 0)
                    {
                        // ー at start with nothing to combine - just remove it
                        sentence.Words.RemoveAt(i);
                        continue;
                    }

                    bool found = false;

                    // Try combining preceding tokens (longest match first)
                    for (int combineCount = Math.Min(4, i); combineCount >= 1; combineCount--)
                    {
                        var combinedTokens = sentence.Words
                                                     .Skip(i - combineCount).Take(combineCount)
                                                     .Select(w => w.word.Text);
                        var combined = string.Concat(combinedTokens);
                        // PRIORITY 1: Try WITH ー (preserves わー, ねー, すげー, etc.)
                        var withBar = combined + "ー";
                        string? withBarHiragana;
                        try
                        {
                            withBarHiragana = WanaKana.ToHiragana(withBar);
                        }
                        catch
                        {
                            withBarHiragana = null;
                        }


                        if (withBarHiragana != null && _lookups.TryGetValue(withBarHiragana, out var idsWithBar) && idsWithBar.Count > 0)
                        {
                            // Found valid word WITH ー - merge and keep ー
                            var firstToken = sentence.Words[i - combineCount];
                            firstToken.word.Text = withBar;
                            // Calculate combined length from all merged tokens including ー
                            int combinedLength = 0;
                            for (int j = i - combineCount; j <= i; j++)
                                combinedLength += sentence.Words[j].length;
                            // Update the tuple with new length
                            sentence.Words[i - combineCount] = (firstToken.word, firstToken.position, combinedLength);
                            sentence.Words.RemoveRange(i - combineCount + 1, combineCount);
                            i = i - combineCount; // Adjust index after removal
                            found = true;
                            break;
                        }

                        // PRIORITY 2: Try WITHOUT ー (handles 久しぶりー → 久しぶり)
                        string? withoutBarHiragana;
                        try
                        {
                            withoutBarHiragana = WanaKana.ToHiragana(combined);
                        }
                        catch
                        {
                            withoutBarHiragana = null;
                        }

                        if (withoutBarHiragana != null && _lookups.TryGetValue(withoutBarHiragana, out var idsWithout) &&
                            idsWithout.Count > 0)
                        {
                            // Found valid word WITHOUT ー - merge and strip ー
                            var firstToken = sentence.Words[i - combineCount];
                            firstToken.word.Text = combined;
                            // Calculate combined length from merged tokens (excluding ー)
                            int combinedLength = 0;
                            for (int j = i - combineCount; j < i; j++)
                                combinedLength += sentence.Words[j].length;
                            // Update the tuple with new length
                            sentence.Words[i - combineCount] = (firstToken.word, firstToken.position, combinedLength);
                            sentence.Words.RemoveRange(i - combineCount + 1, combineCount);
                            i = i - combineCount; // Adjust index after removal
                            found = true;
                            break;
                        }
                    }

                    // PRIORITY 3: Fallback
                    if (!found)
                    {
                        var prevTuple = sentence.Words[i - 1];
                        // If preceding token already ends with ー, this is just emphasis - discard it
                        // Otherwise attach ー to preserve stylistic usage
                        if (!prevTuple.word.Text.EndsWith("ー"))
                        {
                            prevTuple.word.Text += "ー";
                            // Update tuple with new length (previous length + ー length)
                            int newLength = prevTuple.length + sentence.Words[i].length;
                            sentence.Words[i - 1] = (prevTuple.word, prevTuple.position, newLength);
                        }

                        sentence.Words.RemoveAt(i);
                        // No need to adjust i since only 1 item removed at current position
                    }
                }
            }
        }

        /// <summary>
        /// Removes orphaned misparse tokens (single-kana fillers, junk tokens) that weren't consumed
        /// by earlier repair stages. Runs after RepairLongVowelMisparses/RepairLongVowelTokens so those
        /// stages can use filler tokens for backward-merge reconstruction (e.g. お+ま+えー → おまえ).
        /// </summary>
        private static void FilterOrphanedMisparses(List<SentenceInfo> sentences)
        {
            foreach (var sentence in sentences)
            {
                for (int i = sentence.Words.Count - 1; i >= 0; i--)
                {
                    var word = sentence.Words[i].word;

                    bool nextIsLongVowel = i + 1 < sentence.Words.Count &&
                                           sentence.Words[i + 1].word.Text == "ー";

                    bool shouldFilter = MisparsesRemove.Contains(word.Text) &&
                                        !(nextIsLongVowel && word.Text.Length == 1 && WanaKana.IsKana(word.Text)) &&
                                        !word.Text.EndsWith("ー");

                    if (shouldFilter ||
                        word.PartOfSpeech == PartOfSpeech.Noun && !nextIsLongVowel && (
                            (word.Text.Length == 1 && WanaKana.IsKana(word.Text)) ||
                            word.Text is "エナ" or "えな"
                        ))
                    {
                        sentence.Words.RemoveAt(i);
                    }
                }
            }
        }

        /// <summary>
        /// Combines adjacent noun tokens that form valid JMDict entries.
        /// Uses greedy longest-match: tries 4-token, then 3-token, then 2-token combinations.
        /// </summary>
        /// <summary>
        /// Splits noun tokens that have no JMDict lookup into individual kanji characters.
        /// This allows CombineNounCompounds to recombine them with adjacent tokens at better boundaries.
        /// e.g. Sudachi gives 各党 + 幹部, but 各党 has no lookup. Splitting into 各 + 党 lets
        /// CombineNounCompounds find 党幹部 in lookups and produce 各 + 党幹部.
        /// </summary>
        private static void SplitUnknownNounTokens(List<SentenceInfo> sentences)
        {
            foreach (var sentence in sentences)
            {
                if (sentence.Words.Count < 2)
                    continue;

                bool anySplit = false;
                var result = new List<(WordInfo word, int position, int length)>(sentence.Words.Count);

                foreach (var (word, position, length) in sentence.Words)
                {
                    if (word.Text.Length >= 2 &&
                        PosMapper.IsNounForCompounding(word.PartOfSpeech) &&
                        word.Text.All(c => WanaKana.IsKanji(c.ToString())) &&
                        !(_lookups.TryGetValue(word.Text, out var ids) && ids.Count > 0))
                    {
                        for (int j = 0; j < word.Text.Length; j++)
                        {
                            var charText = word.Text[j].ToString();
                            var splitWord = new WordInfo(word)
                                            {
                                                Text = charText, DictionaryForm = charText, NormalizedForm = charText, Reading = ""
                                            };
                            result.Add((splitWord, position + j, 1));
                        }

                        anySplit = true;
                    }
                    else
                    {
                        result.Add((word, position, length));
                    }
                }

                if (anySplit)
                    sentence.Words = result;
            }
        }

        private static readonly HashSet<string> NounCompoundExclusions = ["おつもり"];

        private static void CombineNounCompounds(List<SentenceInfo> sentences)
        {
            foreach (var sentence in sentences)
            {
                if (sentence.Words.Count < 2)
                    continue;

                var result = new List<(WordInfo word, int position, int length)>(sentence.Words.Count);
                int i = 0;

                while (i < sentence.Words.Count)
                {
                    var (word, position, length) = sentence.Words[i];

                    if (!PosMapper.IsNounForCompounding(word.PartOfSpeech))
                    {
                        // When an adjective stem is followed by a noun-like token, try combining them.
                        // Sudachi sometimes splits kana compounds like でかぶつ into でか (形容詞) + ぶつ (接尾辞),
                        // or ながもの into なが (形容詞) + もの (名詞).
                        if (word.PartOfSpeech == PartOfSpeech.IAdjective &&
                            i + 1 < sentence.Words.Count &&
                            PosMapper.IsNounForCompounding(sentence.Words[i + 1].word.PartOfSpeech))
                        {
                            var combinedText = word.Text + sentence.Words[i + 1].word.Text;
                            if (_lookups.TryGetValue(combinedText, out var adjSuffixIds) && adjSuffixIds.Count > 0 &&
                                !adjSuffixIds.All(id => _nameOnlyWordIds.Contains(id)))
                            {
                                var combinedReading = word.Reading + sentence.Words[i + 1].word.Reading;
                                int combinedLength = length + sentence.Words[i + 1].length;
                                var combinedWord = new WordInfo(word)
                                                   {
                                                       Text = combinedText, DictionaryForm = combinedText,
                                                       PartOfSpeech = PartOfSpeech.Noun, NormalizedForm = combinedText,
                                                       Reading = WanaKana.ToHiragana(combinedReading,
                                                           new DefaultOptions { ConvertLongVowelMark = false }),
                                                       PreMatchedWordId = null
                                                   };
                                result.Add((combinedWord, position, combinedLength));
                                i += 2;
                                continue;
                            }
                        }

                        result.Add(sentence.Words[i]);
                        i++;
                        continue;
                    }

                    // Try longest match first (5 tokens, then 4, then 3, then 2)
                    int bestMatch = 1;
                    for (int windowSize = Math.Min(5, sentence.Words.Count - i); windowSize >= 2; windowSize--)
                    {
                        bool allValid = true;
                        bool hasNameLikeToken = false;
                        for (int j = 0; j < windowSize; j++)
                        {
                            var w = sentence.Words[i + j].word;
                            bool isNoun = PosMapper.IsNounForCompounding(w.PartOfSpeech);
                            bool isNoParticle = w.PartOfSpeech == PartOfSpeech.Particle && w.Text == "の"
                                                                                        && j > 0 && j < windowSize - 1;
                            if (!isNoun && !isNoParticle)
                            {
                                allValid = false;
                                break;
                            }

                            if (isNoun && PosMapper.IsNameLikeSudachiNoun(w.PartOfSpeech, w.PartOfSpeechSection1,
                                                                          w.PartOfSpeechSection2, w.PartOfSpeechSection3))
                                hasNameLikeToken = true;
                        }

                        if (!allValid)
                            continue;

                        // Combine the text
                        var combinedText = string.Concat(sentence.Words.Skip(i).Take(windowSize).Select(w => w.word.Text));

                        if (NounCompoundExclusions.Contains(combinedText))
                            continue;

                        // Check if it exists in JMDict lookups — skip name-only matches
                        // when none of the constituent tokens are name-like
                        if (_lookups.TryGetValue(combinedText, out var wordIds) && wordIds.Count > 0 &&
                            (hasNameLikeToken || !wordIds.All(id => _nameOnlyWordIds.Contains(id))))
                        {
                            bestMatch = windowSize;
                            break;
                        }

                        // Also try hiragana version
                        var hiraganaText = WanaKana.ToHiragana(combinedText,
                                                               new DefaultOptions { ConvertLongVowelMark = false });
                        if (hiraganaText != combinedText &&
                            _lookups.TryGetValue(hiraganaText, out wordIds) && wordIds.Count > 0 &&
                            (hasNameLikeToken || !wordIds.All(id => _nameOnlyWordIds.Contains(id))))
                        {
                            bestMatch = windowSize;
                            break;
                        }
                    }

                    if (bestMatch > 1)
                    {
                        // Merge tokens
                        var combinedText = string.Concat(sentence.Words.Skip(i).Take(bestMatch).Select(w => w.word.Text));
                        var combinedReading = string.Concat(sentence.Words.Skip(i).Take(bestMatch).Select(w => w.word.Reading));
                        int combinedLength = 0;
                        for (int j = 0; j < bestMatch; j++)
                        {
                            combinedLength += sentence.Words[i + j].length;
                        }

                        // Preserve Sudachi POS section information when merging.
                        // Sudachi often tokenizes proper names into multiple parts, and we want the combined token to remain name-like.
                        var sectionCarrier = sentence.Words[i].word;
                        for (int j = 0; j < bestMatch; j++)
                        {
                            var candidate = sentence.Words[i + j].word;
                            if (PosMapper.IsNameLikeSudachiNoun(candidate.PartOfSpeech, candidate.PartOfSpeechSection1,
                                                                candidate.PartOfSpeechSection2, candidate.PartOfSpeechSection3))
                            {
                                sectionCarrier = candidate;
                                break;
                            }

                            if (candidate.PartOfSpeechSection1 != PartOfSpeechSection.None ||
                                candidate.PartOfSpeechSection2 != PartOfSpeechSection.None ||
                                candidate.PartOfSpeechSection3 != PartOfSpeechSection.None)
                            {
                                sectionCarrier = candidate;
                                break;
                            }
                        }

                        var combinedWord = new WordInfo(sectionCarrier)
                                           {
                                               Text = combinedText, DictionaryForm = combinedText,
                                               PartOfSpeech = sentence.Words[i].word.PartOfSpeech, NormalizedForm = combinedText, Reading =
                                                   WanaKana.ToHiragana(combinedReading,
                                                                       new DefaultOptions { ConvertLongVowelMark = false }),
                                               PreMatchedWordId = null
                                           };

                        result.Add((combinedWord, position, combinedLength));
                        i += bestMatch;
                    }
                    else
                    {
                        result.Add(sentence.Words[i]);
                        i++;
                    }
                }

                sentence.Words = result;
            }
        }

        /// <summary>
        /// Cleans token text in sentences: removes non-Japanese characters and problematic sequences.
        /// Also removes empty tokens (except SupplementarySymbol boundary markers).
        /// </summary>
        private static void CleanSentenceTokens(List<SentenceInfo> sentences)
        {
            foreach (var sentence in sentences)
            {
                foreach (var (word, _, _) in sentence.Words)
                {
                    // Keep SupplementarySymbol tokens as boundary markers
                    if (word.PartOfSpeech == PartOfSpeech.SupplementarySymbol)
                        continue;

                    word.Text = TokenCleanRegex.Replace(word.Text, "");
                    word.Text = SmallTsuLongVowelRegex.Replace(word.Text, "");
                }

                sentence.Words.RemoveAll(w => string.IsNullOrWhiteSpace(w.word.Text) &&
                                              w.word.PartOfSpeech != PartOfSpeech.SupplementarySymbol);
            }
        }

        private static void CleanCompoundCache()
        {
            if (CompoundExpressionCache.Count < MAX_COMPOUND_CACHE_SIZE)
                return;

            // Evict oldest entries (first added) instead of clearing all
            int toRemove = Math.Min(EVICTION_BATCH_SIZE, CompoundCacheOrder.Count);
            for (int i = 0; i < toRemove; i++)
            {
                var oldest = CompoundCacheOrder.First;
                if (oldest == null)
                    continue;

                CompoundExpressionCache.Remove(oldest.Value);
                CompoundCacheOrder.RemoveFirst();
            }
        }

        private static void AddToCompoundCache(string key, (bool validExpression, int? wordId) value)
        {
            CleanCompoundCache();
            if (CompoundExpressionCache.TryAdd(key, value))
                CompoundCacheOrder.AddLast(key);
        }

        private static List<DeckWord> ExcludeFinalMisparses(IEnumerable<DeckWord> words)
        {
            if (ExcludedMisparses.Count == 0)
                return words.ToList();

            return words.Where(w => !ExcludedMisparses.Contains((w.WordId, w.ReadingIndex))).ToList();
        }

        private static async Task<(int startIndex, string dictionaryForm, int wordId)?> TryMatchCompounds(
            List<WordInfo> wordInfos,
            int wordIndex,
            int lastConsumedIndex = -1)
        {
            var verb = wordInfos[wordIndex];

            var dictForm = verb.DictionaryForm;

            if (string.IsNullOrEmpty(dictForm))
            {
                var deconjugated = Deconjugator.Instance.Deconjugate(WanaKana.ToHiragana(verb.Text));
                if (deconjugated.Count == 0) return null;
                var selectedForm = deconjugated.OrderBy(d => d.Text.Length).ThenBy(d => d.Text, StringComparer.Ordinal).First();
                // If the shortest form is empty (e.g. ない → "" from stem-mizenkei rules),
                // use the original verb text to avoid matching just the prefix (しょうが) instead of the full compound (しょうがない)
                dictForm = string.IsNullOrEmpty(selectedForm.Text) ? verb.Text : selectedForm.Text;
                return await TryMatchCompoundWindow(wordInfos, wordIndex, lastConsumedIndex, dictForm);
            }

            // Try Sudachi's dictionary form first
            var result = await TryMatchCompoundWindow(wordInfos, wordIndex, lastConsumedIndex, dictForm);
            if (result.HasValue) return result;

            // Try intermediate deconjugation forms for compound expressions whose JMDict entry
            // includes a conjugation stem (e.g. 臆病風に吹かれる: dictForm is 吹く but the
            // lookup key contains the passive form 吹かれる)
            if (dictForm != verb.Text && verb.Text.Length > dictForm.Length)
            {
                var deconj = Deconjugator.Instance.Deconjugate(verb.Text);
                var intermediates = new HashSet<string>(StringComparer.Ordinal);
                foreach (var form in deconj)
                {
                    foreach (var seen in form.SeenText)
                    {
                        if (seen.Length > dictForm.Length && seen.Length < verb.Text.Length)
                            intermediates.Add(seen);
                    }
                }

                foreach (var intermediate in intermediates.OrderByDescending(s => s.Length))
                {
                    result = await TryMatchCompoundWindow(wordInfos, wordIndex, lastConsumedIndex, intermediate);
                    if (result.HasValue) return result;
                }
            }

            // Fallback: try deconjugated forms for conjugated compound expressions
            // Only when Sudachi didn't deconjugate (dictForm == surface text) AND the token
            // contains kanji. Pure-kana tokens (いい, じゃない) are already in dictionary form;
            // kanji tokens (明かん) indicate Sudachi genuinely failed to deconjugate.
            if (dictForm == verb.Text && verb.Text.Any(c => WanaKana.IsKanji(c.ToString())))
            {
                var deconj = Deconjugator.Instance.Deconjugate(WanaKana.ToHiragana(verb.Text));
                foreach (var form in deconj
                                     .Select(d => d.Text)
                                     .Where(t => !string.IsNullOrEmpty(t) && t != dictForm)
                                     .Distinct()
                                     .OrderBy(t => t.Length)
                                     .ThenBy(t => t, StringComparer.Ordinal))
                {
                    result = await TryMatchCompoundWindow(wordInfos, wordIndex, lastConsumedIndex, form);
                    if (result.HasValue) return result;
                }
            }

            return null;
        }

        private static async Task<(int startIndex, string dictionaryForm, int wordId)?> TryMatchCompoundWindow(
            List<WordInfo> wordInfos,
            int wordIndex,
            int lastConsumedIndex,
            string dictForm)
        {
            for (int windowSize = Math.Min(5, wordIndex + 1); windowSize >= 2; windowSize--)
            {
                int startIndex = wordIndex - windowSize + 1;

                if (startIndex <= lastConsumedIndex)
                    continue;

                var firstWord = wordInfos[startIndex];
                if (firstWord.PartOfSpeech == PartOfSpeech.Particle)
                    continue;
                if (firstWord.PartOfSpeech == PartOfSpeech.Conjunction && firstWord.Text.Length == 1)
                    continue;
                if (firstWord.WasReclassifiedFromSuffix)
                    continue;

                bool expressionOnly = firstWord.PartOfSpeech == PartOfSpeech.Suffix;

                bool hasPunctuation = false;
                for (int j = startIndex; j < wordIndex; j++)
                {
                    if (wordInfos[j].PartOfSpeech == PartOfSpeech.SupplementarySymbol)
                    {
                        hasPunctuation = true;
                        break;
                    }
                }

                if (hasPunctuation)
                    continue;

                var prefix = string.Concat(wordInfos.Skip(startIndex).Take(windowSize - 1).Select(w => w.Text));
                var candidate = prefix + dictForm;

                if (!expressionOnly)
                {
                    lock (CompoundCacheLock)
                    {
                        if (CompoundExpressionCache.TryGetValue(candidate, out var cached))
                        {
                            if (cached.validExpression)
                                return (startIndex, candidate, cached.wordId!.Value);

                            continue;
                        }
                    }
                }

                if (_lookups.TryGetValue(candidate, out var wordIds) && wordIds.Count > 0)
                {
                    var validWordId = await FindValidCompoundWordId(wordIds, expressionOnly, WanaKana.IsKana(candidate));
                    if (validWordId.HasValue)
                    {
                        lock (CompoundCacheLock)
                        {
                            AddToCompoundCache(candidate, (true, validWordId.Value));
                        }

                        return (startIndex, candidate, validWordId.Value);
                    }
                }

                var hiraganaCandidate = WanaKana.ToHiragana(candidate, new DefaultOptions() { ConvertLongVowelMark = false });
                if (hiraganaCandidate != candidate && _lookups.TryGetValue(hiraganaCandidate, out wordIds) && wordIds.Count > 0)
                {
                    var validWordId = await FindValidCompoundWordId(wordIds, expressionOnly, isKana: true);
                    if (validWordId.HasValue)
                    {
                        lock (CompoundCacheLock)
                        {
                            AddToCompoundCache(candidate, (true, validWordId.Value));
                        }

                        return (startIndex, candidate, validWordId.Value);
                    }
                }

                lock (CompoundCacheLock)
                {
                    AddToCompoundCache(candidate, (false, null));
                }
            }

            return null;
        }

        private static async Task<int?> FindValidCompoundWordId(List<int> wordIds, bool expressionOnly = false, bool isKana = true)
        {
            try
            {
                var wordCache = await JmDictCache.GetWordsAsync(wordIds);

                // First pass: prefer Expression POS (e.g. そうする "to do so" over 奏する "to play music")
                // When multiple expressions match (e.g. 異にする vs 事にする), pick the best by priority
                int? bestExprWordId = null;
                int bestExprScore = int.MinValue;
                foreach (var wordId in wordIds)
                {
                    if (!wordCache.TryGetValue(wordId, out var word)) continue;

                    var posList = word.PartsOfSpeech.ToPartOfSpeech();
                    if (posList.Contains(PartOfSpeech.Expression))
                    {
                        int score = word.GetPriorityScore(isKana);
                        if (score > bestExprScore || (score == bestExprScore && (bestExprWordId == null || wordId < bestExprWordId.Value)))
                        {
                            bestExprScore = score;
                            bestExprWordId = wordId;
                        }
                    }
                }

                if (bestExprWordId.HasValue)
                    return bestExprWordId;

                if (expressionOnly) return null;

                // Second pass: collect all valid compound candidates and pick best by priority
                int? bestWordId = null;
                int bestScore = int.MinValue;
                foreach (var wordId in wordIds)
                {
                    if (!wordCache.TryGetValue(wordId, out var word)) continue;

                    var posList = word.PartsOfSpeech.ToPartOfSpeech();
                    if (posList.Any(PosMapper.IsValidCompoundPOS))
                    {
                        int score = word.GetPriorityScore(isKana);
                        if (score > bestScore || (score == bestScore && (bestWordId == null || wordId < bestWordId.Value)))
                        {
                            bestScore = score;
                            bestWordId = wordId;
                        }
                    }
                }

                return bestWordId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Warning] Error validating compound POS: {ex.Message}");
            }

            return null;
        }

        #region Form-based pair scoring

        private static List<FormCandidate> EnumerateCandidateForms(
            JmDictWord word,
            string targetHiragana,
            bool allowLooseLvmMatch,
            DeconjugationForm? deconjForm = null,
            string? surface = null)
        {
            var candidates = new List<FormCandidate>();
            var targetNormalized = KanaNormalizer.Normalize(targetHiragana);

            foreach (var form in word.Forms)
            {
                if (form.ReadingIndex > 255)
                    continue;

                var formHiragana = WanaKana.ToHiragana(form.Text, new DefaultOptions { ConvertLongVowelMark = false });
                var formNormalized = KanaNormalizer.Normalize(formHiragana);

                bool phoneticMatch = formNormalized == targetNormalized;

                if (!phoneticMatch && allowLooseLvmMatch)
                {
                    var targetLoose = KanaNormalizer.Normalize(WanaKana.ToHiragana(targetHiragana));
                    var formLoose = KanaNormalizer.Normalize(WanaKana.ToHiragana(form.Text));
                    phoneticMatch = formLoose == targetLoose;
                }

                if (!phoneticMatch)
                    continue;

                candidates.Add(new FormCandidate(word, form, (byte)form.ReadingIndex, targetHiragana, deconjForm));
            }

            // Per-word hard filters: drop search-only/obsolete forms only if non-search-only/non-obsolete alternatives exist
            if (candidates.Count <= 1)
                return candidates;

            var nonSearchOnly = candidates.Where(c => !c.Form.IsSearchOnly || c.Form.Text == surface).ToList();
            if (nonSearchOnly.Count > 0)
                candidates = nonSearchOnly;

            var nonObsolete = candidates.Where(c => !c.Form.IsObsolete || c.Form.Text == surface).ToList();
            if (nonObsolete.Count > 0)
                candidates = nonObsolete;

            return candidates;
        }

        private static bool IsSuruNounWithoutExpression(IList<string> posTags)
        {
            bool hasNoun = false, hasSuru = false, hasExpression = false;
            foreach (var tag in posTags)
            {
                if (tag is "n" or "n-adv" or "n-t") hasNoun = true;
                else if (tag is "vs" or "vs-s" or "vs-i" or "vs-c") hasSuru = true;
                else if (tag is "exp") hasExpression = true;
            }

            return hasNoun && hasSuru && !hasExpression;
        }

        private static FormCandidate? PickBestFormCandidate(
            List<FormCandidate> allCandidates,
            string surface,
            string? dictionaryForm,
            string? normalizedForm,
            bool isNameContext,
            ParserDiagnostics? diagnostics = null,
            string? sudachiReading = null)
        {
            var context = FormScoringContext.Create(
                surface,
                dictionaryForm,
                normalizedForm,
                isNameContext,
                sudachiReading);

            return FormCandidateSelector.PickBestCandidate(allCandidates, context, ArchaicPosTypes, diagnostics);
        }

        #endregion
    }
}
