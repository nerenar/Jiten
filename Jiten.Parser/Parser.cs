using System.Diagnostics;
using System.Text.RegularExpressions;
using Jiten.Cli.ML;
using Jiten.Core;
using Jiten.Core.Data;
using Jiten.Core.Data.JMDict;
using Jiten.Core.Utils;
using Jiten.Parser.Data.Redis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WanaKanaShaapu;

namespace Jiten.Parser
{
    public static class Parser
    {
        private static bool _initialized = false;
        private static readonly SemaphoreSlim _initSemaphore = new SemaphoreSlim(1, 1);

        private static readonly bool UseCache = true;
        private static IDeckWordCache DeckWordCache;
        private static IJmDictCache JmDictCache;

        private static JitenDbContext _dbContext;
        private static Dictionary<string, List<int>> _lookups;


        private static async Task InitDictionaries()
        {
            var configuration = new ConfigurationBuilder()
                                .SetBasePath(Directory.GetCurrentDirectory())
                                .AddJsonFile(Path.Combine(Environment.CurrentDirectory, "..", "Shared", "sharedsettings.json"),
                                             optional: true,
                                             reloadOnChange: true)
                                .AddJsonFile("sharedsettings.json", optional: true, reloadOnChange: true)
                                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                                .AddEnvironmentVariables()
                                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<JitenDbContext>();
            optionsBuilder.UseNpgsql(_dbContext.Database.GetConnectionString());

            
            DeckWordCache = new RedisDeckWordCache(configuration);
            JmDictCache = new RedisJmDictCache(configuration, optionsBuilder.Options);

            _lookups = await JmDictHelper.LoadLookupTable(_dbContext);

            // Check if cache is already initialized
            if (!await JmDictCache.IsCacheInitializedAsync())
            {
                // Cache not initialized, load from database and populate Redis
                var allWords = await JmDictHelper.LoadAllWords(_dbContext);

                const int BATCH_SIZE = 10000;

                // // Store lookups in Redis
                // for (int i = 0; i < _lookups.Count; i += BATCH_SIZE)
                // {
                //     var lookupsBatch = _lookups.Skip(i).Take(BATCH_SIZE).ToDictionary(x => x.Key, x => x.Value);
                //     await JmDictCache.SetLookupIdsAsync(lookupsBatch);
                // }

                // Store words in Redis using batching
                for (int i = 0; i < allWords.Count; i += BATCH_SIZE)
                {
                    var wordsBatch = allWords.Skip(i).Take(BATCH_SIZE)
                                             .ToDictionary(w => w.WordId, w => w);
                    await JmDictCache.SetWordsAsync(wordsBatch);
                }

                // Mark cache as initialized
                await JmDictCache.SetCacheInitializedAsync();
            }
        }

        public static async Task<List<DeckWord>> ParseText(JitenDbContext context, string text)
        {
            _dbContext = context;
            if (!_initialized)
            {
                await _initSemaphore.WaitAsync();
                try
                {
                    if (!_initialized) // Double-check to avoid race conditions
                    {
                        await InitDictionaries();
                        _initialized = true;
                    }
                }
                finally
                {
                    _initSemaphore.Release();
                }
            }

            var parser = new MorphologicalAnalyser();
            var sentences = await parser.Parse(text);
            var wordInfos = sentences.SelectMany(s => s.Words).Select(w => w.word).ToList();

            // Only keep kanjis, kanas, digits,full width digits, latin characters, full width latin characters
            foreach (WordInfo wi in wordInfos)
            {
                wi.Text = Regex.Replace(wi.Text,
                                        "[^a-zA-Z0-9\u3040-\u309F\u30A0-\u30FF\u4E00-\u9FAF\uFF21-\uFF3A\uFF41-\uFF5A\uFF10-\uFF19\u3005．]",
                                        "");
            }

            // Remove empty lines
            wordInfos.RemoveAll(x => string.IsNullOrWhiteSpace(x.Text));

            // Filter bad lines that cause exceptions
            wordInfos.ForEach(x => x.Text = Regex.Replace(x.Text, "ッー", ""));

            Deconjugator deconjugator = new Deconjugator();

            const int BATCH_SIZE = 1000;
            List<DeckWord> allProcessedWords = new List<DeckWord>();

            for (int i = 0; i < wordInfos.Count; i += BATCH_SIZE)
            {
                var batch = wordInfos.Skip(i).Take(BATCH_SIZE).ToList();
                var processBatch = batch.Select(word => ProcessWord((word, 0), deconjugator)).ToList();
                var batchResults = await Task.WhenAll(processBatch);

                allProcessedWords.AddRange(batchResults.Where(result => result != null).Select(result => result!));
            }

            return allProcessedWords;
        }

        public static async Task<Deck> ParseTextToDeck(JitenDbContext context, string text,
                                                       bool storeRawText = false,
                                                       bool predictDifficulty = true,
                                                       MediaType mediatype = MediaType.Novel)
        {
            _dbContext = context;
            if (!_initialized)
            {
                await _initSemaphore.WaitAsync();
                try
                {
                    if (!_initialized) // Double-check to avoid race conditions
                    {
                        await InitDictionaries();
                        _initialized = true;
                    }
                }
                finally
                {
                    _initSemaphore.Release();
                }
            }

            var timer = new Stopwatch();
            timer.Start();
            var parser = new MorphologicalAnalyser();
            var sentences = await parser.Parse(text);
            var wordInfos = sentences.SelectMany(s => s.Words).Select(w => w.word).ToList();

            // TODO: support elongated vowels ふ～ -> ふう

            // Only keep kanjis, kanas, digits,full width digits, latin characters, full width latin characters 
            wordInfos.ForEach(x => x.Text =
                                  Regex.Replace(x.Text,
                                                "[^a-zA-Z0-9\u3040-\u309F\u30A0-\u30FF\u4E00-\u9FAF\uFF21-\uFF3A\uFF41-\uFF5A\uFF10-\uFF19\u3005．]",
                                                ""));
            // Remove empty lines
            wordInfos = wordInfos.Where(x => !string.IsNullOrWhiteSpace(x.Text)).ToList();

            // Filter bad lines that cause exceptions
            // wordInfos.RemoveAll(w => w.Text is "ッー");
            wordInfos.ForEach(x => x.Text = Regex.Replace(x.Text, "ッー", ""));

            Deconjugator deconjugator = new Deconjugator();

            var uniqueWords = new List<(WordInfo wordInfo, int occurrences)>();
            var wordCount = new Dictionary<(string, PartOfSpeech), int>();

            foreach (var word in wordInfos)
            {
                if (!wordCount.TryAdd((word.Text, word.PartOfSpeech), 1))
                    wordCount[(word.Text, word.PartOfSpeech)]++;
                else
                    uniqueWords.Add((word, 1));
            }

            for (int i = 0; i < uniqueWords.Count; i++)
            {
                uniqueWords[i] = (uniqueWords[i].wordInfo, wordCount[(uniqueWords[i].wordInfo.Text, uniqueWords[i].wordInfo.PartOfSpeech)]);
            }

            timer.Stop();
            double mecabTime = timer.Elapsed.TotalMilliseconds;

            timer.Restart();

            const int BATCH_SIZE = 1000;
            List<DeckWord> allProcessedWords = new List<DeckWord>();

            for (int i = 0; i < uniqueWords.Count; i += BATCH_SIZE)
            {
                var batch = uniqueWords.Skip(i).Take(BATCH_SIZE).ToList();
                var processBatch = batch.Select(word => ProcessWord(word, deconjugator)).ToList();
                var batchResults = await Task.WhenAll(processBatch);

                allProcessedWords.AddRange(batchResults.Where(result => result != null).Select(result => result!));
            }

            var processedWords = allProcessedWords.ToArray();

            processedWords = processedWords
                             .Select(result => result)
                             .ToArray();

            // Assign the occurences for each word
            foreach (var deckWord in processedWords)
            {
                // Remove one since we already counted it at the start
                deckWord.Occurrences--;
                deckWord.Occurrences +=
                    processedWords.Count(x => x.WordId == deckWord.WordId && x.ReadingIndex == deckWord.ReadingIndex);
            }

            // deduplicate deconjugated words
            processedWords = processedWords
                             .GroupBy(x => new { x.WordId, x.ReadingIndex })
                             .Select(x => x.First())
                             .ToArray();

            List<ExampleSentence>? exampleSentences = null;

            if (mediatype is MediaType.Novel or MediaType.NonFiction or MediaType.VideoGame or MediaType.VisualNovel or MediaType.WebNovel)
                exampleSentences = ExampleSentenceExtractor.ExtractSentences(sentences, processedWords);

            // Split into sentences
            // string[] sentences = Regex.Split(text, @"(?<=[。！？」）])|(?<=[…])\r\n");
            // sentences = sentences.Select(sentence =>
            // {
            //     // Find the first Japanese character
            //     Match match = Regex.Match(sentence, @"[\p{IsHiragana}\p{IsKatakana}\p{IsCJKUnifiedIdeographs}]");
            //     if (match.Success)
            //     {
            //         int startIndex = match.Index;
            //         // Remove all special characters
            //         return Regex.Replace(sentence.Substring(startIndex),
            //                              "[^a-zA-Z0-9\u3040-\u309F\u30A0-\u30FF\u4E00-\u9FAF\uFF21-\uFF3A\uFF41-\uFF5A\uFF10-\uFF19\u3005]",
            //                              "");
            //     }
            //
            //     return "";
            // }).Where(s => !string.IsNullOrEmpty(s)).ToArray();

            timer.Stop();

            double deconjugationTime = timer.Elapsed.TotalMilliseconds;

            double totalTime = mecabTime + deconjugationTime;

            Console.WriteLine("Total words found : " + wordInfos.Count);

            // Console.WriteLine("Unique words found before deconjugation : " + uniqueWordInfos.Count);
            Console.WriteLine("Unique words found after deconjugation : " + processedWords.Length);

            var characterCount = wordInfos.Sum(x => x.Text.Length);
            Console.WriteLine($"Time elapsed: {totalTime:0.0}ms");
            Console.WriteLine($"Mecab time: {mecabTime:0.0}ms ({(mecabTime / totalTime * 100):0}%), Deconjugation time: {deconjugationTime:0.0}ms ({(deconjugationTime / totalTime * 100):0}%)");

            // Character count
            Console.WriteLine("Character count: " + characterCount);
            // Time for 10000 characters
            Console.WriteLine($"Time per 10000 characters: {(totalTime / characterCount * 10000):0.0}ms");
            // Time for 1million characters
            Console.WriteLine($"Time per 1 million characters: {(totalTime / characterCount * 1000000):0.0}ms");

            var textWithoutDialogues = Regex.Replace(text, @"[「『].{0,200}?[」』]", "", RegexOptions.Singleline);
            textWithoutDialogues = Regex.Replace(textWithoutDialogues,
                                                 "[^a-zA-Z0-9\u3040-\u309F\u30A0-\u30FF\u4E00-\u9FAF\uFF21-\uFF3A\uFF41-\uFF5A\uFF10-\uFF19\u3005．]",
                                                 "");
            var textWithoutPunctuation = Regex.Replace(text,
                                                       "[^a-zA-Z0-9\u3040-\u309F\u30A0-\u30FF\u4E00-\u9FAF\uFF21-\uFF3A\uFF41-\uFF5A\uFF10-\uFF19\u3005．]",
                                                       "");

            int dialogueCharacterCount = textWithoutPunctuation.Length - textWithoutDialogues.Length;
            float dialoguePercentage = (float)dialogueCharacterCount / textWithoutPunctuation.Length * 100f;

            Console.WriteLine($"Dialogue percentage: {dialoguePercentage:0.0}%");

            var deck = new Deck
                       {
                           CharacterCount = characterCount, WordCount = wordInfos.Count, UniqueWordCount = processedWords.Length,
                           UniqueWordUsedOnceCount = processedWords.Count(x => x.Occurrences == 1),
                           UniqueKanjiCount = wordInfos.SelectMany(w => w.Text).Distinct().Count(c => WanaKana.IsKanji(c.ToString())),
                           UniqueKanjiUsedOnceCount = wordInfos.SelectMany(w => w.Text).GroupBy(c => c)
                                                               .Count(g => g.Count() == 1 && WanaKana.IsKanji(g.Key.ToString())),
                           SentenceCount = sentences.Count, DialoguePercentage = dialoguePercentage, DeckWords = processedWords,
                           RawText = storeRawText ? new DeckRawText(text) : null, ExampleSentences = exampleSentences
                       };

            if (predictDifficulty)
            {
                DifficultyPredictor difficultyPredictor =
                    new(_dbContext.DbOptions,
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "difficulty_prediction_model.onnx"));
                deck.Difficulty = await difficultyPredictor.PredictDifficulty(deck, mediatype);
                Console.WriteLine($"Predicted difficulty: {deck.Difficulty}");
            }


            return deck;
        }

        public static async Task<List<DeckWord?>> ParseMorphenes(JitenDbContext context, string text)
        {
            _dbContext = context;
            if (!_initialized)
            {
                await _initSemaphore.WaitAsync();
                try
                {
                    if (!_initialized) // Double-check to avoid race conditions
                    {
                        await InitDictionaries();
                        _initialized = true;
                    }
                }
                finally
                {
                    _initSemaphore.Release();
                }
            }

            var parser = new MorphologicalAnalyser();
            var sentences = await parser.Parse(text, morphemesOnly: true);
            var wordInfos = sentences.SelectMany(s => s.Words).Select(w => w.word).ToList();

            // Filter bad lines that cause exceptions
            wordInfos.ForEach(x => x.Text = Regex.Replace(x.Text, "ッー", ""));

            Deconjugator deconjugator = new Deconjugator();

            const int BATCH_SIZE = 1000;
            List<DeckWord?> allProcessedWords = new();

            for (int i = 0; i < wordInfos.Count; i += BATCH_SIZE)
            {
                var batch = wordInfos.Skip(i).Take(BATCH_SIZE).ToList();
                var processBatch = batch.Select(word => ProcessWord((word, 0), deconjugator)).ToList();
                var batchResults = await Task.WhenAll(processBatch);

                allProcessedWords.AddRange(batchResults);
            }

            return allProcessedWords;
        }

        private static async Task<DeckWord?> ProcessWord((WordInfo wordInfo, int occurrences) wordData, Deconjugator deconjugator)
        {
            var cacheKey = new DeckWordCacheKey(
                                                wordData.wordInfo.Text,
                                                wordData.wordInfo.PartOfSpeech,
                                                wordData.wordInfo.DictionaryForm
                                               );

            if (UseCache)
            {
                var cachedWord = await DeckWordCache.GetAsync(cacheKey);

                if (cachedWord != null)
                {
                    return new DeckWord
                           {
                               WordId = cachedWord.WordId, OriginalText = wordData.wordInfo.Text, ReadingIndex = cachedWord.ReadingIndex,
                               Occurrences = wordData.occurrences, Conjugations = cachedWord.Conjugations,
                               PartsOfSpeech = cachedWord.PartsOfSpeech, Origin = cachedWord.Origin
                           };
                }
            }

            DeckWord? processedWord = null;
            bool isProcessed = false;

            do
            {
                if (wordData.wordInfo.PartOfSpeech is PartOfSpeech.Verb or PartOfSpeech.IAdjective
                        or PartOfSpeech.NaAdjective || wordData.wordInfo.PartOfSpeechSection1 is PartOfSpeechSection.Adjectival)
                {
                    // Try to deconjugate as verb or adjective
                    var verbResult = await DeconjugateVerbOrAdjective(wordData, deconjugator);
                    if (!verbResult.success || verbResult.word == null)
                    {
                        // The word might be a noun misparsed as a verb/adjective like お祭り
                        var nounResult = await DeconjugateWord(wordData);
                        processedWord = nounResult.word;
                    }
                    else
                    {
                        processedWord = verbResult.word;
                    }
                }
                else
                {
                    var result = await DeconjugateWord(wordData);
                    processedWord = result.word;
                }

                if (processedWord != null) break;

                // We haven't found a match, let's try to remove the last character if it's a っ, a ー or a duplicate
                if (wordData.wordInfo.Text.Length > 2 &&
                    (wordData.wordInfo.Text[^1] == 'っ' || wordData.wordInfo.Text[^1] == 'ー' ||
                     wordData.wordInfo.Text[^2] == wordData.wordInfo.Text[^1]))
                {
                    wordData.wordInfo.Text = wordData.wordInfo.Text[..^1];
                }
                // Let's try to remove any honorifics in front of the word
                else if (wordData.wordInfo.Text.StartsWith("お"))
                {
                    wordData.wordInfo.Text = wordData.wordInfo.Text[1..];
                }
                // Let's try without any long vowel mark
                else if (wordData.wordInfo.Text.Contains("ー"))
                {
                    wordData.wordInfo.Text = wordData.wordInfo.Text.Replace("ー", "");
                }
                else
                {
                    isProcessed = true;
                }
            } while (!isProcessed);

            if (processedWord != null)
            {
                processedWord.Occurrences = wordData.occurrences;

                if (UseCache)
                {
                    await DeckWordCache.SetAsync(cacheKey,
                                                 new DeckWord
                                                 {
                                                     WordId = processedWord.WordId, OriginalText = processedWord.OriginalText,
                                                     ReadingIndex = processedWord.ReadingIndex, Conjugations = processedWord.Conjugations,
                                                     PartsOfSpeech = processedWord.PartsOfSpeech, Origin = processedWord.Origin
                                                 });
                }
            }

            return processedWord;
        }

        private static async Task<(bool success, DeckWord? word)> DeconjugateWord((WordInfo wordInfo, int occurrences) wordData)
        {
            string text = wordData.wordInfo.Text;

            // Exclude full digits or single latin character
            if (text.All(char.IsDigit) || (text.Length == 1 && text.IsAsciiOrFullWidthLetter()))
            {
                return (false, null);
            }

            _lookups.TryGetValue(text, out List<int>? candidates);
            var textInHiragana = WanaKana.ToHiragana(wordData.wordInfo.Text, new DefaultOptions { ConvertLongVowelMark = false });
            _lookups.TryGetValue(textInHiragana, out var candidatesHiragana);

            // Add candidatesInHiragana to candidates, deduplicate 
            if (candidatesHiragana is { Count: not 0 })
            {
                candidates ??= new List<int>();
                var newCandidates = new List<int>(candidates);
                newCandidates.AddRange(candidatesHiragana);
                candidates = newCandidates.Distinct().ToList();
            }

            if (candidates is { Count: not 0 })
            {
                candidates = candidates.OrderBy(c => c).ToList();

                var wordCache = await JmDictCache.GetWordsAsync(candidates);
                
                List<JmDictWord> matches = new();
                JmDictWord? bestMatch = null;

                foreach (var id in candidates)
                {
                    if (!wordCache.TryGetValue(id, out var word)) continue;

                    List<PartOfSpeech> pos = word.PartsOfSpeech.ToPartOfSpeech();
                    if (!pos.Contains(wordData.wordInfo.PartOfSpeech)) continue;

                    matches.Add(word);
                }

                if (matches.Count == 0)
                {
                    if (wordCache.TryGetValue(candidates[0], out var fallbackWord))
                        bestMatch = fallbackWord;
                    else
                        return (true, null);
                }
                
                else if (matches.Count > 1)
                    bestMatch = matches.OrderByDescending(m => m.GetPriorityScore(WanaKana.IsKana(wordData.wordInfo.Text))).First();
                else
                    bestMatch = matches[0];

                var normalizedReadings =
                    bestMatch.Readings.ToList();
                byte readingIndex = (byte)normalizedReadings.IndexOf(text);

                // not found, try with hiragana form
                if (readingIndex == 255)
                {
                    var normalizedHiraganaReadings =
                        bestMatch.Readings.Select(r => WanaKana.ToHiragana(r, new DefaultOptions() { ConvertLongVowelMark = false }))
                                 .ToList();
                    readingIndex = (byte)normalizedHiraganaReadings.IndexOf(textInHiragana);
                }

                // not found, try with converting the long vowel mark
                if (readingIndex == 255)
                {
                    normalizedReadings =
                        bestMatch.Readings.Select(r => WanaKana.ToHiragana(r)).ToList();
                    readingIndex = (byte)normalizedReadings.IndexOf(textInHiragana);
                }

                // Still not found, skip the word completely
                if (readingIndex == 255)
                {
                    return (false, null);
                }

                DeckWord deckWord = new()
                                    {
                                        WordId = bestMatch.WordId, OriginalText = wordData.wordInfo.Text, ReadingIndex = readingIndex,
                                        Occurrences = wordData.occurrences, PartsOfSpeech = bestMatch.PartsOfSpeech.ToPartOfSpeech(),
                                        Origin = bestMatch.Origin
                                    };
                return (true, deckWord);
            }

            return (false, null);
        }

        private static async Task<(bool success, DeckWord? word)> DeconjugateVerbOrAdjective(
            (WordInfo wordInfo, int occurrences) wordData, Deconjugator deconjugator)
        {
            var deconjugated = deconjugator.Deconjugate(WanaKana.ToHiragana(wordData.wordInfo.Text))
                                           .OrderByDescending(d => d.Text.Length).ToList();

            List<(DeconjugationForm form, List<int> ids)> candidates = new();
            foreach (var form in deconjugated)
            {
                if (_lookups.TryGetValue(form.Text, out List<int> lookup))
                {
                    candidates.Add((form, lookup));
                }
            }

            if (candidates.Count == 0)
            {
                return (true, null);
            }

            // if there's a candidate that's the same as the base word, put it first in the list
            var baseDictionaryWord = WanaKana.ToHiragana(wordData.wordInfo.DictionaryForm);
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
            if (baseWordIndex != -1)
            {
                var baseWordCandidate = candidates[baseWordIndex];
                candidates.RemoveAt(baseWordIndex);
                candidates.Insert(0, baseWordCandidate);
            }
            
            var allCandidateIds = candidates.SelectMany(c => c.ids).Distinct().ToList();

            if (!allCandidateIds.Any())
                return (false, null);

            var wordCache = await JmDictCache.GetWordsAsync(allCandidateIds);
            
            List<(JmDictWord word, DeconjugationForm form)> matches = new();
            (JmDictWord word, DeconjugationForm form) bestMatch;

            foreach (var candidate in candidates)
            {
                foreach (var id in candidate.ids)
                {
                    if (!wordCache.TryGetValue(id, out var word)) continue;

                    List<PartOfSpeech> pos = word.PartsOfSpeech.ToPartOfSpeech();
                    if (!pos.Contains(wordData.wordInfo.PartOfSpeech)) continue;

                    matches.Add((word, candidate.form));
                }
            }

            if (matches.Count == 0)
            {
                return (false, null);
            }

            if (matches.Count > 1)
            {
                bestMatch = matches.OrderByDescending(m => m.Item1.GetPriorityScore(WanaKana.IsKana(wordData.wordInfo.Text))).First();

                if (!WanaKana.IsKana(wordData.wordInfo.NormalizedForm))
                {
                    foreach (var match in matches)
                    {
                        if (match.word.Readings.Any(r => r == wordData.wordInfo.NormalizedForm))
                        {
                            bestMatch = match;
                            break;
                        }
                    }
                }
            }
            else
                bestMatch = matches[0];

            var normalizedReadings =
                bestMatch.word.Readings.Select(r => WanaKana.ToHiragana(r, new DefaultOptions() { ConvertLongVowelMark = false })).ToList();
            byte readingIndex = (byte)normalizedReadings.IndexOf(bestMatch.form.Text);

            // not found, try with converting the long vowel mark
            if (readingIndex == 255)
            {
                normalizedReadings =
                    bestMatch.word.Readings.Select(r => WanaKana.ToHiragana(r)).ToList();
                readingIndex = (byte)normalizedReadings.IndexOf(bestMatch.form.Text);
            }

            DeckWord deckWord = new()
                                {
                                    WordId = bestMatch.word.WordId, OriginalText = wordData.wordInfo.Text, ReadingIndex = readingIndex,
                                    Occurrences = wordData.occurrences, Conjugations = bestMatch.form.Process,
                                    PartsOfSpeech = bestMatch.word.PartsOfSpeech.ToPartOfSpeech(), Origin = bestMatch.word.Origin
                                };
            return (true, deckWord);
        }
    }
}