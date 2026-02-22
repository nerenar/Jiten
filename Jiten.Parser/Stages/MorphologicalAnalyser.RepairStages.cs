using Jiten.Core.Data;
using Jiten.Core.Utils;
using WanaKanaShaapu;

namespace Jiten.Parser;

public partial class MorphologicalAnalyser
{
    private List<WordInfo> RepairTankaToTaNKa(List<WordInfo> wordInfos)
    {
        var result = new List<WordInfo>(wordInfos.Count + 4);
        var deconj = Deconjugator.Instance;

        for (int i = 0; i < wordInfos.Count; i++)
        {
            var word = wordInfos[i];

            // Only process たんか noun tokens
            if (word.PartOfSpeech != PartOfSpeech.Noun || word.Text != "たんか")
            {
                result.Add(word);
                continue;
            }

            // Don't split if followed by を (object marker - indicates real noun usage like たんかを吐く)
            if (i + 1 < wordInfos.Count && wordInfos[i + 1].Text == "を")
            {
                result.Add(word);
                continue;
            }

            // Don't split if preceded by を (indicates real noun)
            if (result.Count > 0 && result[^1].Text == "を")
            {
                result.Add(word);
                continue;
            }

            // Don't split if preceded by の (possessive - indicates real noun like お島の方のたんか)
            if (result.Count > 0 && result[^1].Text == "の")
            {
                result.Add(word);
                continue;
            }

            // Helper to find the last meaningful token (skip punctuation)
            WordInfo? GetPrevToken(int offset = 1)
            {
                int count = 0;
                for (int j = result.Count - 1; j >= 0; j--)
                {
                    if (result[j].PartOfSpeech == PartOfSpeech.SupplementarySymbol) continue;
                    count++;
                    if (count == offset) return result[j];
                }

                return null;
            }

            int GetPrevTokenIndex(int offset = 1)
            {
                int count = 0;
                for (int j = result.Count - 1; j >= 0; j--)
                {
                    if (result[j].PartOfSpeech == PartOfSpeech.SupplementarySymbol) continue;
                    count++;
                    if (count == offset) return j;
                }

                return -1;
            }

            // Check if splitting would create a valid verb conjugation
            bool shouldSplit = false;
            var prev = GetPrevToken(1);

            if (prev != null)
            {
                // Pattern 1: Verb/Adjective + たんか → Verb/Adjective + た + ん + か
                // e.g., 云う + たんか → 云うた + ん + か (valid past tense)
                // e.g., 怖がって + たんか → 怖がってた + ん + か (te-form + ta)
                if (prev.PartOfSpeech == PartOfSpeech.Verb)
                {
                    var candidateText = prev.Text + "た";
                    var forms = deconj.Deconjugate(NormalizeToHiragana(candidateText));
                    if (forms.Any(f => f.Tags.Any(t => t.StartsWith("v"))))
                        shouldSplit = true;
                }

                // Pattern 1b: Te-form ending + たんか → combine with た
                // Handles cases like 怖がって + たんか where 怖がって is classified as IAdjective
                // If prev ends with て/で, adding た creates てた/でた (past progressive/resultative)
                if (!shouldSplit && (prev.Text.EndsWith("て") || prev.Text.EndsWith("で")))
                {
                    // This is likely a te-form that should combine with た from たんか
                    // e.g., 怖がって + た → 怖がってた (was scared)
                    shouldSplit = true;
                }

                // Pattern 2: Adverb もう + たんか → もう is part of てもうた (Kansai てしまった)
                // e.g., ハズレて + もう + たんか → ハズレてもうた + ん + か
                // Check by text "もう" since POS might vary
                if (prev.Text == "もう")
                {
                    var verbBefore = GetPrevToken(2);
                    if (verbBefore != null && (verbBefore.Text.EndsWith("て") || verbBefore.Text.EndsWith("で")))
                    {
                        // Combine: verbて + もう + た → verbてもうた
                        var combinedText = verbBefore.Text + "もうた";
                        var prevIdx = GetPrevTokenIndex(1);
                        var verbIdx = GetPrevTokenIndex(2);
                        // Remove in descending order to keep indices valid
                        if (prevIdx >= 0 && verbIdx >= 0)
                        {
                            if (prevIdx > verbIdx)
                            {
                                result.RemoveAt(prevIdx);
                                result.RemoveAt(verbIdx);
                            }
                            else
                            {
                                result.RemoveAt(verbIdx);
                                result.RemoveAt(prevIdx);
                            }
                        }

                        result.Add(new WordInfo(verbBefore) { Text = combinedText, PartOfSpeech = PartOfSpeech.Verb });
                        result.Add(CreateNToken());
                        result.Add(new WordInfo { Text = "か", DictionaryForm = "か", PartOfSpeech = PartOfSpeech.Particle, Reading = "か" });
                        continue;
                    }
                }

                // Pattern 3: も + たんか after し (conjunction) → part of てしもた (Kansai てしまった)
                // e.g., 言うて + し + も + たんか → 言うてしもた + ん + か
                if (prev.Text == "も")
                {
                    var shiToken = GetPrevToken(2);
                    if (shiToken != null && shiToken.Text == "し")
                    {
                        var verbBefore = GetPrevToken(3);
                        if (verbBefore != null && (verbBefore.Text.EndsWith("て") || verbBefore.Text.EndsWith("で") ||
                                                   verbBefore.PartOfSpeech == PartOfSpeech.Expression))
                        {
                            // Combine: verb + し + も + た → verbしもた
                            var combinedText = verbBefore.Text + "しもた";
                            var moIdx = GetPrevTokenIndex(1);
                            var shiIdx = GetPrevTokenIndex(2);
                            var verbIdx = GetPrevTokenIndex(3);
                            // Remove in descending index order
                            var indices = new[] { moIdx, shiIdx, verbIdx }.Where(x => x >= 0).OrderByDescending(x => x).ToList();
                            foreach (var idx in indices) result.RemoveAt(idx);
                            result.Add(new WordInfo(verbBefore) { Text = combinedText, PartOfSpeech = PartOfSpeech.Verb });
                            result.Add(CreateNToken());
                            result.Add(new WordInfo
                                       {
                                           Text = "か", DictionaryForm = "か", PartOfSpeech = PartOfSpeech.Particle, Reading = "か"
                                       });
                            continue;
                        }
                    }
                }
            }

            if (shouldSplit && prev != null)
            {
                // Modify previous verb to include た
                var prevIdx = GetPrevTokenIndex(1);
                if (prevIdx >= 0)
                {
                    result[prevIdx] = new WordInfo(prev) { Text = prev.Text + "た", PartOfSpeech = PartOfSpeech.Verb };
                }

                result.Add(CreateNToken());
                result.Add(new WordInfo { Text = "か", DictionaryForm = "か", PartOfSpeech = PartOfSpeech.Particle, Reading = "か" });
            }
            else
            {
                result.Add(word);
            }
        }

        return result;
    }

    private List<WordInfo> RepairVowelElongation(List<WordInfo> wordInfos)
    {
        if (wordInfos.Count < 2) return wordInfos;

        var deconjugator = Deconjugator.Instance;
        var result = new List<WordInfo>(wordInfos.Count);

        static WordInfo MakeInterjection(string text) =>
            new()
            {
                Text = text, DictionaryForm = text, NormalizedForm = text, Reading = text, PartOfSpeech = PartOfSpeech.Interjection
            };

        static bool IsVerbPast(List<DeconjugationForm> forms) =>
            forms.Any(f => f.Tags.Any(t => t.StartsWith("v", StringComparison.Ordinal)) && f.Process.Any(p => p == "past"));

        static bool IsRuVerb(List<DeconjugationForm> forms, string expectedDictionaryHiragana) =>
            forms.Any(f => f.Text == expectedDictionaryHiragana && f.Tags.Any(t => t is "v1" or "v5r"));

        for (int i = 0; i < wordInfos.Count; i++)
        {
            var current = wordInfos[i];

            if (result.Count == 0)
            {
                result.Add(current);
                continue;
            }

            var prev = result[^1];

            // Pattern: [noun] + [んー filler] → merge ん into preceding token, discard ー
            // Sudachi splits Xん+ー as X + んー when ー causes the filler interpretation
            // e.g., 総ちゃんー → 総 + ちゃ(noun) + んー(filler) → 総 + ちゃん
            if (current.PartOfSpeech is PartOfSpeech.Interjection or PartOfSpeech.Filler &&
                current.Text.Length >= 2 &&
                current.Text[0] == 'ん' &&
                current.Text[1..].All(c => c == 'ー') &&
                prev.PartOfSpeech is PartOfSpeech.Noun or PartOfSpeech.CommonNoun &&
                prev.Text.Length <= 2 &&
                !prev.Text.EndsWith("ん"))
            {
                result[^1] = new WordInfo(prev) { Text = prev.Text + "ん", PartOfSpeech = PartOfSpeech.Suffix };
                continue;
            }

            // Pattern 0: [prefix] + [る OOV] + [ー symbol]
            // Sudachi splits る-verbs when followed by expressive elongation ー
            // e.g., 来るー → 来(prefix) + る(OOV noun) + ー(symbol)
            if (current.PartOfSpeech == PartOfSpeech.SupplementarySymbol && current.Text == "ー" &&
                result.Count >= 2 &&
                prev is { Text: "る", PartOfSpeech: PartOfSpeech.Noun } &&
                result[^2].PartOfSpeech == PartOfSpeech.Prefix)
            {
                var prefix = result[^2];
                var verbText = prefix.Text + "る";
                result.RemoveAt(result.Count - 1);
                result[^1] = new WordInfo(prefix)
                {
                    Text = verbText, DictionaryForm = verbText, NormalizedForm = verbText,
                    PartOfSpeech = PartOfSpeech.Verb
                };
                continue;
            }

            // Pattern 1: Token ending in "るう" that might be a misparsed verb + elongation
            // e.g., "かるう" could be part of "ぶつかる" + "う"
            if (current.Text.EndsWith("るう", StringComparison.Ordinal) && current.Text.Length >= 2)
            {
                var verbCandidate = prev.Text + current.Text[..^1]; // prev + current minus trailing う
                var verbHiragana = NormalizeToHiragana(verbCandidate);

                // Check if this forms a valid る-verb by testing negative form deconjugation.
                // Godan-ru verbs use らない (ぶつかる → ぶつからない), ichidan verbs use ない (食べる → 食べない).
                // Validate by requiring the deconjugator to recover the exact candidate (hiragana) as v1 or v5r.
                var isValidRuVerb = verbHiragana.EndsWith("る", StringComparison.Ordinal) &&
                                    (IsRuVerb(deconjugator.Deconjugate(verbHiragana[..^1] + "ない"), verbHiragana) ||
                                     IsRuVerb(deconjugator.Deconjugate(verbHiragana[..^1] + "らない"), verbHiragana));

                if (isValidRuVerb)
                {
                    // Replace the previous token with the combined verb
                    result[^1] = new WordInfo(prev)
                                 {
                                     Text = verbCandidate, DictionaryForm = verbCandidate, NormalizedForm = verbCandidate,
                                     Reading = WanaKana.ToHiragana(prev.Reading + current.Text[..^1]), PartOfSpeech = PartOfSpeech.Verb
                                 };
                    // Add the elongation う as a separate token
                    result.Add(MakeInterjection("う"));
                    continue;
                }
            }

            // Pattern 3: Token + "たあ" (often misparsed as particle と)
            // e.g., "おき" + "たあ" should be "おきた" + "あ" (past of 起きる)
            if (current.Text == "たあ")
            {
                var pastCandidate = prev.Text + "た";
                var pastHiragana = NormalizeToHiragana(pastCandidate);

                // Check if this forms a valid verb past tense
                var isValidVerbPast = IsVerbPast(deconjugator.Deconjugate(pastHiragana));

                if (isValidVerbPast)
                {
                    result[^1] = new WordInfo(prev) { Text = pastCandidate, Reading = WanaKana.ToHiragana(prev.Reading + "た"), PartOfSpeech = PartOfSpeech.Verb };
                    result.Add(MakeInterjection("あ"));
                    continue;
                }
            }

            // Pattern 4: Token ending in "た" + "ああ" (interjection)
            // e.g., "いきた" + "ああ" where いきた is misparsed as nominal adjective
            if (current.Text == "ああ")
            {
                var prevHiragana = NormalizeToHiragana(prev.Text);

                // Check if prev token ending in た is a valid verb past tense
                if (prevHiragana.EndsWith("た", StringComparison.Ordinal) || prevHiragana.EndsWith("だ", StringComparison.Ordinal))
                {
                    if (IsVerbPast(deconjugator.Deconjugate(prevHiragana)) && prev.PartOfSpeech != PartOfSpeech.Verb)
                    {
                        result[^1] = new WordInfo(prev) { PartOfSpeech = PartOfSpeech.Verb };
                        // Keep ああ but as interjection (it already is, so just add it)
                    }
                }
            }

            result.Add(current);
        }

        return result;
    }

    private List<WordInfo> RepairNTokenisation(List<WordInfo> wordInfos)
    {
        if (wordInfos.Count < 2) return wordInfos;

        // Phase 1: Split compound tokens that Sudachi incorrectly grouped
        var split = new List<WordInfo>(wordInfos.Count + 4);
        foreach (var word in wordInfos)
        {
            // Split tokens starting with ん (e.g., んだ → ん + だ)
            if (word.Text.Length > 1 && word.Text[0] == 'ん')
            {
                var remainder = word.Text[1..];
                if (NCompoundSuffixes.Contains(remainder) || NCompoundSuffixes.Any(s => remainder.StartsWith(s)))
                {
                    var nToken = CreateNToken();
                    if (word.PartOfSpeech == PartOfSpeech.Interjection)
                        nToken.DictionaryForm = "の";
                    split.Add(nToken);
                    split.Add(new WordInfo(word)
                    {
                        Text = remainder, DictionaryForm = remainder,
                        NormalizedForm = remainder, Reading = remainder
                    });
                    continue;
                }
            }

            // Split tokens starting with だ when preceded by ん (e.g., だが → だ + が)
            if (word.Text.Length > 1 && word.Text[0] == 'だ' &&
                split.Count > 0 && (split[^1].Text == "ん" || split[^1].Text.EndsWith("ん")))
            {
                var remainder = word.Text[1..];
                if (DaCompoundSuffixes.Contains(remainder))
                {
                    split.Add(CreateDaToken());
                    split.Add(new WordInfo(word)
                    {
                        Text = remainder, DictionaryForm = remainder,
                        NormalizedForm = remainder, Reading = remainder
                    });
                    continue;
                }
            }

            // Split そうだ → そう + だ (appearance/hearsay pattern should be split for combining logic)
            if (word is { Text: "そうだ", PartOfSpeech: PartOfSpeech.Adverb })
            {
                split.Add(new WordInfo(word)
                          {
                              Text = "そう", DictionaryForm = "そう", NormalizedForm = "そう", Reading = "そう",
                              PartOfSpeech = PartOfSpeech.Auxiliary, PartOfSpeechSection1 = PartOfSpeechSection.AuxiliaryVerbStem
                          });
                split.Add(CreateDaToken());
                continue;
            }

            split.Add(word);
        }

        // Phase 2: Recombine verb stems with ん using deconjugator validation
        var result = new List<WordInfo>(split.Count);
        var deconj = Deconjugator.Instance;

        for (int i = 0; i < split.Count; i++)
        {
            var current = split[i];

            // Case: Token already ends with ん (e.g., 飲ん) and next is だ/で - combine as past/te-form
            // Skip na-adjectives (e.g., たくさん + で should NOT combine - で is the copula, not verb conjugation)
            // Skip suffixes (e.g., さん + だ should NOT combine - さん is honorific, だ is copula)
            if (current.Text.EndsWith("ん") && current.Text.Length > 1 && current.Text != "ん" &&
                !IsNaAdjectiveToken(current) &&
                current.PartOfSpeech != PartOfSpeech.Suffix &&
                !NormalizeToHiragana(current.DictionaryForm).EndsWith("ん") &&
                i + 1 < split.Count && split[i + 1].Text is "だ" or "で")
            {
                var candidateText = current.Text + split[i + 1].Text;
                if (IsNdaVerbForm(deconj.Deconjugate(NormalizeToHiragana(candidateText))))
                {
                    var candidateReading = WanaKana.ToHiragana(current.Reading + split[i + 1].Reading);
                    result.Add(new WordInfo(current)
                    {
                        Text = candidateText, PartOfSpeech = PartOfSpeech.Verb,
                        NormalizedForm = candidateText, Reading = candidateReading
                    });
                    i++;
                    continue;
                }
            }

            // Case: Standalone ん - try to combine with preceding verb stem
            if (current.Text == "ん" && result.Count > 0)
            {
                bool combined = false;

                // Try んだ/んで pattern (past/te-form) - only for verb conjugation, not explanatory ん
                // Skip when ん is explanatory particle (DictionaryForm = "の" or "ん") or negative auxiliary (DictionaryForm = "ぬ")
                if (i + 1 < split.Count && split[i + 1].Text is "だ" or "で" &&
                    current.DictionaryForm is not "ぬ" and not "の" and not "ん")
                {
                    var suffix = "ん" + split[i + 1].Text;
                    var suffixReading = "ん" + split[i + 1].Reading;
                    if (TryCombineWithLookback(result, suffix, suffixReading, deconj, IsNdaVerbForm, out var combinedWord))
                    {
                        result.Add(combinedWord!);
                        combined = true;
                        i++;
                    }
                }

                // Fallback for ん classified as explanatory (from interjection split):
                // Sudachi sometimes misparsed verb stems as nouns (e.g., 喜んだだろうね → 喜(noun) + んだ)
                // Validate via dictionary lookup that noun + ぶ/む/ぬ/ぐ is a real verb
                if (!combined && i + 1 < split.Count && split[i + 1].Text is "だ" or "で" &&
                    current.DictionaryForm is "の" or "ん" &&
                    result.Count > 0 && result[^1].PartOfSpeech is PartOfSpeech.Noun or PartOfSpeech.CommonNoun &&
                    HasCompoundLookup != null)
                {
                    var prev = result[^1];
                    string[] ndaVerbEndings = ["ぶ", "む", "ぬ", "ぐ"];
                    foreach (var ending in ndaVerbEndings)
                    {
                        if (HasCompoundLookup(prev.Text + ending) ||
                            HasCompoundLookup(NormalizeToHiragana(prev.Text) + ending))
                        {
                            var candidateText = prev.Text + "ん" + split[i + 1].Text;
                            var candidateReading = WanaKana.ToHiragana(prev.Reading + "ん" + split[i + 1].Reading);
                            result.RemoveAt(result.Count - 1);
                            result.Add(new WordInfo(prev)
                            {
                                Text = candidateText, PartOfSpeech = PartOfSpeech.Verb,
                                NormalizedForm = candidateText, Reading = candidateReading,
                                PartOfSpeechSection1 = PartOfSpeechSection.None,
                                PartOfSpeechSection2 = PartOfSpeechSection.None,
                                PartOfSpeechSection3 = PartOfSpeechSection.None
                            });
                            combined = true;
                            i++;
                            break;
                        }
                    }
                }

                // If んだ/んで didn't match, try negative ん contraction (ませ+ん→ません)
                // Only for actual negative auxiliary (DictionaryForm = "ぬ"), not explanatory ん
                if (!combined && current.DictionaryForm == "ぬ" &&
                    TryCombineWithLookback(result, "ん", "ん", deconj, IsAnyVerbForm, out var negativeWord))
                {
                    // After combining ませ+ん→ません, try to combine preceding verb stem with ません
                    // e.g., [し, ませ] + ん → [しません]
                    if (negativeWord!.Text.EndsWith("ません") && result.Count > 0)
                    {
                        var verbStem = result[^1];
                        var candidateText = verbStem.Text + negativeWord.Text;
                        var candidateHiragana = NormalizeToHiragana(candidateText);
                        var forms = deconj.Deconjugate(candidateHiragana);
                        if (IsMasenVerbForm(forms))
                        {
                            result.RemoveAt(result.Count - 1);
                            negativeWord.Text = candidateText;
                            negativeWord.DictionaryForm = verbStem.DictionaryForm;
                            negativeWord.NormalizedForm = candidateText;
                            negativeWord.Reading = WanaKana.ToHiragana(verbStem.Reading + negativeWord.Reading);
                        }
                    }

                    result.Add(negativeWord);
                    combined = true;
                }

                if (!combined)
                    result.Add(current);
                continue;
            }

            result.Add(current);
        }

        return result;
    }

    private List<WordInfo> ProcessSpecialCases(List<WordInfo> wordInfos)
    {
        if (wordInfos.Count == 0)
            return wordInfos;

        List<WordInfo> newList = new List<WordInfo>(wordInfos.Count);


        for (int i = 0; i < wordInfos.Count;)
        {
            WordInfo w1 = wordInfos[i];

            if (w1 is { PartOfSpeech: PartOfSpeech.Conjunction or PartOfSpeech.Auxiliary, Text: "で" })
            {
                bool nextIsMo = i + 1 < wordInfos.Count && wordInfos[i + 1].Text == "も";
                if (!nextIsMo)
                {
                    w1.PartOfSpeech = PartOfSpeech.Particle;
                    newList.Add(w1);
                    i++;
                    continue;
                }
            }

            // Sudachi sometimes classifies verb te-forms ending in んで/んだ as 表現 (Expression)
            // when a JMDict expression entry exists (e.g., 飛んで → 2248530 "zero; flying").
            // Reclassify as Verb with the correct DictionaryForm so the parser matches the verb.
            if (w1.PartOfSpeech == PartOfSpeech.Expression && w1.Text.Length >= 3
                && (w1.Text.EndsWith("んで") || w1.Text.EndsWith("んだ")))
            {
                var hiragana = NormalizeToHiragana(w1.Text);
                var deconjForms = Deconjugator.Instance.Deconjugate(hiragana);
                var verbForm = deconjForms.FirstOrDefault(f =>
                    f.Tags.Any(t => t is "v5b" or "v5m" or "v5n" or "v5g") &&
                    (f.Text.EndsWith("ぶ") || f.Text.EndsWith("む") || f.Text.EndsWith("ぬ") || f.Text.EndsWith("ぐ")));
                if (verbForm != null)
                {
                    var prefix = w1.Text[..^2];
                    w1.PartOfSpeech = PartOfSpeech.Verb;
                    w1.DictionaryForm = prefix + verbForm.Text[^1];
                }
            }

            // Sudachi misclassifies standalone ぬ as the archaic verb 寝(ぬ) (文語下二段-ナ行)
            // instead of the classical negative auxiliary ぬ (助動詞-ヌ, NormalizedForm ず)
            if (w1 is { Text: "ぬ", PartOfSpeech: PartOfSpeech.Verb, NormalizedForm: "寝る" })
            {
                w1.PartOfSpeech = PartOfSpeech.Auxiliary;
                w1.NormalizedForm = "ず";
            }

            if (w1 is { PartOfSpeech: PartOfSpeech.Prefix, Text: "今" })
            {
                w1.PartOfSpeech = PartOfSpeech.Adverb;
                newList.Add(w1);
                i++;
                continue;
            }

            // 空 as 形状詞/ウツロ (utsuro) → noun/カラ (kara, "empty")
            // Sudachi misclassifies 空 as na-adjective うつろ, but kanji 空 in modern Japanese
            // almost always reads から (empty) — うつろ is typically written 虚ろ
            if (w1 is { Text: "空", PartOfSpeech: PartOfSpeech.NaAdjective, Reading: "ウツロ" })
            {
                w1.PartOfSpeech = PartOfSpeech.Noun;
                w1.Reading = "カラ";
                w1.NormalizedForm = "空";
                newList.Add(w1);
                i++;
                continue;
            }

            // Combine 形状詞的 suffixes (げ) with preceding adjective stem
            // e.g., 幼(adj-stem) + げ(suffix/形状詞的) → 幼げ
            // Keep as IAdjective so な handler doesn't incorrectly merge (的な, がちな stay unchanged)
            if (w1.PartOfSpeech == PartOfSpeech.Suffix
                && w1.HasPartOfSpeechSection(PartOfSpeechSection.NaAdjectiveLike)
                && newList.Count > 0
                && newList[^1].PartOfSpeech == PartOfSpeech.IAdjective
                && !newList[^1].Text.EndsWith("い"))
            {
                newList[^1].Text += w1.Text;
                i++;
                continue;
            }

            if (i < wordInfos.Count - 2)
            {
                WordInfo w2 = wordInfos[i + 1];
                WordInfo w3 = wordInfos[i + 2];

                bool found = false;
                foreach (var sc in SpecialCases3)
                {
                    if (w1.Text == sc.Item1 && w2.Text == sc.Item2 && w3.Text == sc.Item3)
                    {
                        // Check if preceding token + this expression forms a compound verb
                        // e.g., 板 + に+つい+て → 板につく (idiomatic compound)
                        if (newList.Count > 0 && HasCompoundLookup != null &&
                            w2.PartOfSpeech == PartOfSpeech.Verb)
                        {
                            var prevWord = newList[^1];
                            var compoundDictForm = prevWord.Text + sc.Item1 + w2.DictionaryForm;
                            if (HasCompoundLookup(compoundDictForm))
                            {
                                newList.RemoveAt(newList.Count - 1);
                                var compoundWord = new WordInfo(prevWord);
                                compoundWord.Text = prevWord.Text + w1.Text + w2.Text + w3.Text;
                                compoundWord.DictionaryForm = compoundDictForm;
                                compoundWord.PartOfSpeech = PartOfSpeech.Verb;
                                newList.Add(compoundWord);
                                i += 3;
                                found = true;
                                break;
                            }
                        }

                        var newWord = new WordInfo(w1);
                        newWord.Text = w1.Text + w2.Text + w3.Text;
                        newWord.DictionaryForm = newWord.Text;

                        if (sc.Item4 != null)
                        {
                            newWord.PartOfSpeech = sc.Item4.Value;
                        }

                        newList.Add(newWord);
                        i += 3;
                        found = true;
                        break;
                    }
                }

                if (found)
                    continue;

                // Special case: な + ん + だ should become なんだ (explanatory)
                // BUT only when not preceded by AuxiliaryVerbStem (like そう in 泣きそうな)
                // or NaAdjective (like 好き in 好きなんだ)
                if (w1.Text == "な" && w2.Text == "ん" && w3.Text == "だ")
                {
                    bool prevIsAuxVerbStem = i > 0 &&
                                             wordInfos[i - 1].HasPartOfSpeechSection(PartOfSpeechSection.AuxiliaryVerbStem);
                    bool prevIsNaAdjective = i > 0 &&
                                             wordInfos[i - 1].PartOfSpeech == PartOfSpeech.NaAdjective;
                    if (!prevIsAuxVerbStem && !prevIsNaAdjective)
                    {
                        var newWord = new WordInfo(w1) { Text = "なんだ", DictionaryForm = "なんだ", PartOfSpeech = PartOfSpeech.Auxiliary };
                        newList.Add(newWord);
                        i += 3;
                        continue;
                    }
                }

                // Special case: な + ん (explanatory) when NOT followed by だ
                // e.g., そうなんじゃない → そう + なん + じゃない
                // Only when ん is 準体助詞 (explanatory particle)
                if (w1.Text == "な" && w2.Text == "ん" && w3.Text != "だ" &&
                    w2.HasPartOfSpeechSection(PartOfSpeechSection.Juntaijoushi))
                {
                    bool prevIsAuxVerbStem = i > 0 &&
                                             wordInfos[i - 1].HasPartOfSpeechSection(PartOfSpeechSection.AuxiliaryVerbStem);
                    bool prevIsNaAdjective = i > 0 &&
                                             wordInfos[i - 1].PartOfSpeech == PartOfSpeech.NaAdjective;
                    if (!prevIsAuxVerbStem && !prevIsNaAdjective)
                    {
                        var newWord = new WordInfo(w1) { Text = "なん", DictionaryForm = "なん", PartOfSpeech = PartOfSpeech.Auxiliary };
                        newList.Add(newWord);
                        i += 2;
                        continue;
                    }
                }
            }

            if (i < wordInfos.Count - 1)
            {
                WordInfo w2 = wordInfos[i + 1];

                // Special case: ん + だ + DaCompoundSuffix should become ん + だ[suffix]
                // e.g., 飲んだけど → 飲ん + だけど (verb ん)
                // BUT: そうなんだけど → そう + なんだ + けど (explanatory ん - 準体助詞)
                // Only apply this for non-explanatory ん (not a 準体助詞 particle)
                bool isExplanatoryN = w1.PartOfSpeech == PartOfSpeech.Particle &&
                                      w1.HasPartOfSpeechSection(PartOfSpeechSection.Juntaijoushi);
                if (w1.Text == "ん" && w2.Text == "だ" && i + 2 < wordInfos.Count &&
                    DaCompoundSuffixes.Contains(wordInfos[i + 2].Text) &&
                    !isExplanatoryN)
                {
                    var w3 = wordInfos[i + 2];
                    newList.Add(w1); // Keep ん separate
                    var daSuffix = new WordInfo(w2) { Text = w2.Text + w3.Text, PartOfSpeech = PartOfSpeech.Conjunction };
                    newList.Add(daSuffix);
                    i += 3;
                    continue;
                }

                // に + しろ → にしろ (particle "even if") only when preceded by a verb/adjective/auxiliary
                // e.g., 行くにしろ → 行く + にしろ (whether one goes...)
                // Skip when preceded by a noun: 大概にしろ → 大概 + に + しろ (imperative of 大概にする)
                if (w1.Text == "に" && w2.Text == "しろ")
                {
                    bool prevIsNoun = i > 0 && wordInfos[i - 1].PartOfSpeech is PartOfSpeech.Noun
                        or PartOfSpeech.NaAdjective or PartOfSpeech.Pronoun;
                    if (!prevIsNoun)
                    {
                        var newWord = new WordInfo(w1) { Text = "にしろ", DictionaryForm = "にしろ", PartOfSpeech = PartOfSpeech.Expression };
                        newList.Add(newWord);
                        i += 2;
                        continue;
                    }
                }

                // Sudachi splits はぐれる into は(particle) + ぐれる(verb) after で
                if (w1 is { Text: "は", PartOfSpeech: PartOfSpeech.Particle }
                    && w2 is { PartOfSpeech: PartOfSpeech.Verb, DictionaryForm: "ぐれる" })
                {
                    w2.Text = "は" + w2.Text;
                    w2.DictionaryForm = "はぐれる";
                    w2.NormalizedForm = "はぐれる";
                    w2.Reading = "ハ" + w2.Reading;
                    newList.Add(w2);
                    i += 2;
                    continue;
                }

                // Sudachi misparsing verb stem + とくと as verb + 篤と (adverb)
                // Should be verb + とく (ておく contraction, auxiliary) + と (particle)
                // e.g., 見とくと → 見 + とく + と, 食べとくと → 食べ + とく + と
                if (w1.PartOfSpeech == PartOfSpeech.Verb &&
                    w2 is { PartOfSpeech: PartOfSpeech.Adverb, Text: "とくと", NormalizedForm: "篤と" })
                {
                    newList.Add(w1);
                    newList.Add(new WordInfo
                    {
                        Text = "とく", DictionaryForm = "とく", NormalizedForm = "とく",
                        PartOfSpeech = PartOfSpeech.Auxiliary, Reading = "トク"
                    });
                    newList.Add(new WordInfo
                    {
                        Text = "と", DictionaryForm = "と", NormalizedForm = "と",
                        PartOfSpeech = PartOfSpeech.Particle, Reading = "ト"
                    });
                    i += 2;
                    continue;
                }

                // Sudachi sometimes splits slang/informal godan ラ行 verbs as noun + って (particle)
                // e.g., シコ(noun) + って(particle) should be シコって (te-form of シコる)
                if (w1.PartOfSpeech == PartOfSpeech.Noun && w2.Text == "って" &&
                    w2.PartOfSpeech == PartOfSpeech.Particle && HasCompoundLookup != null)
                {
                    var candidateVerb = w1.Text + "る";
                    if (HasCompoundLookup(candidateVerb))
                    {
                        var newWord = new WordInfo(w1)
                        {
                            Text = w1.Text + w2.Text,
                            DictionaryForm = candidateVerb,
                            PartOfSpeech = PartOfSpeech.Verb,
                            NormalizedForm = candidateVerb,
                            Reading = w1.Reading + w2.Reading
                        };
                        newList.Add(newWord);
                        i += 2;
                        continue;
                    }
                }

                bool found = false;
                foreach (var sc in SpecialCases2)
                {
                    if (w1.Text == sc.Item1 && w2.Text == sc.Item2
                        && !(sc.Item3 == PartOfSpeech.Verb && w1.PartOfSpeech == PartOfSpeech.Conjunction))
                    {
                        var newWord = new WordInfo(w1) { Text = w1.Text + w2.Text };

                        // For verb merges where the first token is a conjugated verb form,
                        // preserve the original dictionary form (e.g., し+て → して with DictionaryForm=する)
                        // This enables compound lookups like 手にする to work correctly
                        if (sc.Item3 == PartOfSpeech.Verb &&
                            !string.IsNullOrEmpty(w1.DictionaryForm) &&
                            w1.DictionaryForm != w1.Text)
                        {
                            newWord.DictionaryForm = w1.DictionaryForm;
                        }
                        else
                        {
                            newWord.DictionaryForm = newWord.Text;
                        }

                        if (sc.Item3 != null)
                        {
                            newWord.PartOfSpeech = sc.Item3.Value;
                        }

                        newList.Add(newWord);
                        i += 2;
                        found = true;
                        break;
                    }
                }

                if (found)
                    continue;
            }

            // This word is (sometimes?) parsed as auxiliary for some reason
            if (w1.Text == "でしょう")
            {
                var newWord = new WordInfo(w1);
                newWord.PartOfSpeech = PartOfSpeech.Expression;
                newWord.PartOfSpeechSection1 = PartOfSpeechSection.None;

                newList.Add(newWord);
                i++;
                continue;
            }


            if (w1.Text == "だし" && w1.PartOfSpeech != PartOfSpeech.Verb && newList.Count > 0)
            {
                var da = new WordInfo
                         {
                             Text = "だ", DictionaryForm = "だ", PartOfSpeech = PartOfSpeech.Auxiliary,
                             PartOfSpeechSection1 = PartOfSpeechSection.None, Reading = "だ"
                         };
                var shi = new WordInfo
                          {
                              Text = "し", DictionaryForm = "し", PartOfSpeech = PartOfSpeech.Conjunction,
                              PartOfSpeechSection1 = PartOfSpeechSection.None, Reading = "し"
                          };

                newList.Add(da);
                newList.Add(shi);
                i++;
                continue;
            }

            // Handle な based on context
            if (w1 is { Text: "な", DictionaryForm: "だ" })
            {
                bool followedByN = i + 1 < wordInfos.Count && wordInfos[i + 1].Text == "ん";

                // If followed by explanatory ん pattern (な + ん + だ), combine into なんだ
                // e.g., 好き + な + ん + だ → 好き + なんだ
                // Also includes quotative particle と: 好き + な + ん + だ + と → 好き + なんだと
                if (newList.Count > 0 && IsNaAdjectiveToken(newList[^1]) && followedByN)
                {
                    // Build "なんだ" by combining な + ん + plain copula だ only
                    // Don't consume conjectural だろ/だろう — those are separate grammar points
                    string combined = "な" + wordInfos[i + 1].Text;
                    int j = i + 2;
                    if (j < wordInfos.Count && wordInfos[j].Text == "だ" && wordInfos[j].PartOfSpeech == PartOfSpeech.Auxiliary)
                    {
                        combined += wordInfos[j].Text;
                        j++;
                    }

                    // Also include quotative particle と if it immediately follows
                    if (j < wordInfos.Count && wordInfos[j].Text == "と" && wordInfos[j].PartOfSpeech == PartOfSpeech.Particle)
                    {
                        combined += wordInfos[j].Text;
                        j++;
                    }

                    w1.Text = combined;
                    w1.DictionaryForm = combined;
                    w1.PartOfSpeech = PartOfSpeech.Auxiliary;
                    newList.Add(w1);
                    i = j;
                    continue;
                }

                // If previous token is na-adjective and NOT followed by ん, combine with na-adjective
                // e.g., 大切 + な → 大切な, 静か + な + 部屋 → 静かな + 部屋
                // BUT: Exclude AuxiliaryVerbStem (like そう in 降りそうな) - keep な separate for learning
                if (newList.Count > 0 && IsNaAdjectiveToken(newList[^1]) && !followedByN
                    && !newList[^1].HasPartOfSpeechSection(PartOfSpeechSection.AuxiliaryVerbStem))
                {
                    newList[^1].Text += w1.Text;
                    i++;
                    continue;
                }

                // Otherwise, treat as particle (not the vegetable 菜)
                w1.PartOfSpeech = PartOfSpeech.Particle;
            }
            // Always process に as the particle and not the baggage
            else if (w1.Text == "に")
                w1.PartOfSpeech = PartOfSpeech.Particle;

            // Always process よう as the noun
            if (w1.Text is "よう")
                w1.PartOfSpeech = PartOfSpeech.Noun;

            if (w1.Text is "十五")
                w1.PartOfSpeech = PartOfSpeech.Numeral;

            if (w1.Text is "オレ")
                w1.PartOfSpeech = PartOfSpeech.Pronoun;

            newList.Add(w1);
            i++;
        }

        return newList;
    }

    /// <summary>
    /// Repairs orphaned conjugation fragments that follow nouns due to Sudachi incorrectly
    /// merging a noun+verb compound into a single noun token.
    /// Handles two patterns:
    /// 1. Orphaned voice auxiliary: 足蹴(noun) + られた(aux) → 足(noun) + 蹴られた(verb)
    /// 2. Orphaned verb ending: 肉食(noun) + う(filler) → 肉(noun) + 食う(verb)
    /// Uses a backward-looking window on the noun to find a valid verb stem via JMDict lookup.
    /// </summary>
    private List<WordInfo> RepairOrphanedAuxiliary(List<WordInfo> wordInfos)
    {
        if (wordInfos.Count < 2 || HasCompoundLookup == null)
            return wordInfos;

        var result = new List<WordInfo>(wordInfos.Count + 2);

        for (int i = 0; i < wordInfos.Count; i++)
        {
            var word = wordInfos[i];

            if (i == 0)
            {
                result.Add(word);
                continue;
            }

            bool isOrphanedAuxiliary = word.PartOfSpeech == PartOfSpeech.Auxiliary
                                       && VerbIndicatingAuxiliaries.Contains(word.DictionaryForm);
            bool isOrphanedVerbEnding = !isOrphanedAuxiliary
                                        && word.Text.Length == 1
                                        && GodanVerbEndings.Contains(word.Text);

            if (!isOrphanedAuxiliary && !isOrphanedVerbEnding)
            {
                result.Add(word);
                continue;
            }

            var prev = result[^1];
            if (prev.PartOfSpeech != PartOfSpeech.Noun || prev.Text.Length < 2)
            {
                result.Add(word);
                continue;
            }

            int maxWindow = Math.Min(prev.Text.Length - 1, 3);
            bool repaired = false;

            for (int w = 1; w <= maxWindow && !repaired; w++)
            {
                string verbStem = prev.Text[^w..];

                if (!verbStem.Any(c => c >= '\u4E00' && c <= '\u9FAF'))
                    continue;

                if (isOrphanedVerbEnding)
                {
                    string dictForm = verbStem + word.Text;
                    string nounRemainder = prev.Text[..^w];
                    if (HasCompoundLookup(dictForm) && HasCompoundLookup(nounRemainder))
                    {
                        ApplyNounVerbSplit(prev, word, nounRemainder, verbStem, dictForm, result);
                        repaired = true;
                    }
                }
                else
                {
                    foreach (var ending in GodanVerbEndings)
                    {
                        string dictForm = verbStem + ending;
                        string nounRemainder = prev.Text[..^w];
                        if (HasCompoundLookup(dictForm) && HasCompoundLookup(nounRemainder))
                        {
                            ApplyNounVerbSplit(prev, word, nounRemainder, verbStem, dictForm, result);
                            repaired = true;
                            break;
                        }
                    }
                }
            }

            if (!repaired)
                result.Add(word);
        }

        return result;
    }

    private static void ApplyNounVerbSplit(
        WordInfo noun, WordInfo orphan, string nounRemainder, string verbStem, string dictForm, List<WordInfo> result)
    {
        int w = noun.Text.Length - nounRemainder.Length;
        noun.Text = nounRemainder;
        if (noun.DictionaryForm.EndsWith(verbStem))
            noun.DictionaryForm = noun.DictionaryForm[..^w];
        if (noun.NormalizedForm.EndsWith(verbStem))
            noun.NormalizedForm = noun.NormalizedForm[..^w];

        result.Add(new WordInfo
        {
            Text = verbStem + orphan.Text,
            DictionaryForm = dictForm,
            NormalizedForm = dictForm,
            PartOfSpeech = PartOfSpeech.Verb,
        });
    }

}
