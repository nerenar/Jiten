using Jiten.Core.Data;
using WanaKanaShaapu;

namespace Jiten.Parser;

public partial class MorphologicalAnalyser
{
    /// <summary>
    /// Splits compound verb tokens that Sudachi outputs as single tokens when they contain auxiliary verbs.
    /// For example: し終わっ (dict: し終わる) → し + 終わっ
    /// This is necessary because compound verbs like し終わる don't exist in JMDict, but their components do.
    /// </summary>
    private List<WordInfo> SplitCompoundAuxiliaryVerbs(List<WordInfo> wordInfos)
    {
        var result = new List<WordInfo>(wordInfos.Count + 4);

        foreach (var word in wordInfos)
        {
            // Only process verb tokens with dictionary forms
            if (word.PartOfSpeech != PartOfSpeech.Verb ||
                string.IsNullOrEmpty(word.DictionaryForm) ||
                word.DictionaryForm.Length < 3)
            {
                result.Add(word);
                continue;
            }

            // Check if dictionary form ends with any auxiliary verb
            string? matchedAux = null;
            foreach (var aux in AuxiliaryVerbs)
            {
                if (word.DictionaryForm.EndsWith(aux) && word.DictionaryForm.Length > aux.Length)
                {
                    matchedAux = aux;
                    break;
                }
            }

            if (matchedAux == null)
            {
                result.Add(word);
                continue;
            }

            // Calculate the main verb prefix length from dictionary form
            int mainVerbDictLen = word.DictionaryForm.Length - matchedAux.Length;
            string mainVerbDict = word.DictionaryForm[..mainVerbDictLen];

            // The surface form should have the same prefix length for the main verb
            // e.g., し終わっ → し (1 char) + 終わっ (3 chars)
            if (word.Text.Length <= mainVerbDictLen)
            {
                result.Add(word);
                continue;
            }

            string mainVerbSurface = word.Text[..mainVerbDictLen];
            string auxVerbSurface = word.Text[mainVerbDictLen..];

            // Verify the auxiliary surface starts with the auxiliary stem
            if (!AuxiliaryVerbStems.TryGetValue(matchedAux, out var auxStem) ||
                !auxVerbSurface.StartsWith(auxStem))
            {
                result.Add(word);
                continue;
            }

            // Create the main verb token
            var mainVerb = new WordInfo
                           {
                               Text = mainVerbSurface, DictionaryForm = mainVerbDict, NormalizedForm = mainVerbDict,
                               PartOfSpeech = PartOfSpeech.Verb, Reading = WanaKana.ToHiragana(mainVerbSurface)
                           };

            // Create the auxiliary verb token
            var auxVerb = new WordInfo
                          {
                              Text = auxVerbSurface, DictionaryForm = matchedAux, NormalizedForm = matchedAux,
                              PartOfSpeech = PartOfSpeech.Verb, PartOfSpeechSection1 = PartOfSpeechSection.PossibleDependant,
                              Reading = WanaKana.ToHiragana(auxVerbSurface)
                          };

            result.Add(mainVerb);
            result.Add(auxVerb);
        }

        return result;
    }

    /// <summary>
    /// Splits the conjunctive particle たって/だって into た/だ (past auxiliary) + って (quotative particle)
    /// when it follows a verb in 連用形 (infinitive/stem form).
    /// Sudachi treats たって as a single 接続助詞 but it should be た + って for proper deconjugation.
    /// Examples: 出たって → 出 + た + って, 行ったって → 行っ + た + って
    /// </summary>
    private List<WordInfo> SplitTatteParticle(List<WordInfo> wordInfos)
    {
        if (wordInfos.Count < 2) return wordInfos;

        var result = new List<WordInfo>(wordInfos.Count + 2);

        for (int i = 0; i < wordInfos.Count; i++)
        {
            var word = wordInfos[i];

            // Split だな misparsed as 棚 (shelf) → だ (copula) + な (particle)
            if (word.Text == "だな" && word.PartOfSpeech == PartOfSpeech.Noun && word.NormalizedForm == "棚")
            {
                result.Add(new WordInfo { Text = "だ", DictionaryForm = "だ", NormalizedForm = "だ", PartOfSpeech = PartOfSpeech.Auxiliary, Reading = "だ" });
                result.Add(new WordInfo { Text = "な", DictionaryForm = "な", NormalizedForm = "な", PartOfSpeech = PartOfSpeech.Particle, PartOfSpeechSection1 = PartOfSpeechSection.SentenceEndingParticle, Reading = "な" });
                continue;
            }

            // Check if this is たって/だって as a conjunctive particle following a verb
            if (i > 0 &&
                word.PartOfSpeech == PartOfSpeech.Particle &&
                word.HasPartOfSpeechSection(PartOfSpeechSection.ConjunctionParticle) &&
                word.Text is "たって" or "だって")
            {
                var prev = wordInfos[i - 1];

                // Only split if preceded by verb/adjective in a stem form (連用形 or similar)
                if (prev.PartOfSpeech is PartOfSpeech.Verb or PartOfSpeech.IAdjective or PartOfSpeech.Auxiliary)
                {
                    // Determine which past marker to use
                    string pastMarker = word.Text == "たって" ? "た" : "だ";

                    // Add the past auxiliary verb (た/だ)
                    result.Add(new WordInfo
                    {
                        Text = pastMarker,
                        DictionaryForm = pastMarker,
                        NormalizedForm = pastMarker,
                        PartOfSpeech = PartOfSpeech.Auxiliary,
                        Reading = pastMarker
                    });

                    // Add the quotative particle (って)
                    result.Add(new WordInfo
                    {
                        Text = "って",
                        DictionaryForm = "って",
                        NormalizedForm = "って",
                        PartOfSpeech = PartOfSpeech.Particle,
                        PartOfSpeechSection1 = PartOfSpeechSection.ConjunctionParticle,
                        Reading = "って"
                    });

                    continue;
                }
            }

            result.Add(word);
        }

        return result;
    }
}
