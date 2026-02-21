using Jiten.Core.Data;
using Jiten.Core.Utils;
using WanaKanaShaapu;

namespace Jiten.Parser;

public partial class MorphologicalAnalyser
{
    private bool CompoundExistsInLookup(string compoundForm, Func<string, List<DeconjugationForm>> cachedDeconjugate)
    {
        if (HasCompoundLookup!(compoundForm))
            return true;

        foreach (var form in cachedDeconjugate(compoundForm))
        {
            if (HasCompoundLookup(form.Text))
                return true;
        }

        return false;
    }

    private List<WordInfo> CombineVerbDependants(List<WordInfo> wordInfos)
    {
        if (wordInfos.Count < 2)
            return wordInfos;

        List<WordInfo> newList = new List<WordInfo>();
        WordInfo currentWord = new WordInfo(wordInfos[0]);

        for (int i = 1; i < wordInfos.Count; i++)
        {
            WordInfo nextWord = wordInfos[i];

            if (nextWord.HasPartOfSpeechSection(PartOfSpeechSection.Dependant) &&
                currentWord.PartOfSpeech == PartOfSpeech.Verb &&
                nextWord.DictionaryForm != "おる" &&
                nextWord.Text != currentWord.Text)
            {
                currentWord.Text += nextWord.Text;
            }
            else
            {
                newList.Add(currentWord);
                currentWord = new WordInfo(nextWord);
            }
        }

        newList.Add(currentWord);
        return newList;
    }

    private List<WordInfo> CombineVerbPossibleDependants(List<WordInfo> wordInfos)
    {
        if (wordInfos.Count < 2)
            return wordInfos;

        List<WordInfo> newList = new List<WordInfo>();
        WordInfo currentWord = new WordInfo(wordInfos[0]);

        for (int i = 1; i < wordInfos.Count; i++)
        {
            WordInfo nextWord = wordInfos[i];

            // Condition uses accumulator (verb) and next word (possible dependant + specific forms)
            // Note: きる is intentionally excluded because it creates compound verbs (e.g., 食べきる)
            // that are often not in JMDict, causing lookup failures. Keep them separate for better parsing.
            if (nextWord.HasPartOfSpeechSection(PartOfSpeechSection.PossibleDependant) &&
                currentWord.PartOfSpeech == PartOfSpeech.Verb && !currentWord.Text.EndsWith("たり") &&
                nextWord.Text != currentWord.Text &&
                nextWord.DictionaryForm is "得る" or "する" or "しまう" or "こなす" or "いく" or "貰う" or "いる" or "ない" or "だす")
            {
                currentWord.Text += nextWord.Text;
            }
            else
            {
                newList.Add(currentWord);
                currentWord = new WordInfo(nextWord);
            }
        }

        newList.Add(currentWord);
        return newList;
    }

    private List<WordInfo> CombineVerbDependantsSuru(List<WordInfo> wordInfos)
    {
        if (wordInfos.Count < 2)
            return wordInfos;

        List<WordInfo> newList = new List<WordInfo>();
        int i = 0;
        while (i < wordInfos.Count)
        {
            WordInfo currentWord = wordInfos[i];

            if (i + 1 < wordInfos.Count)
            {
                WordInfo nextWord = wordInfos[i + 1];
                if (currentWord.HasPartOfSpeechSection(PartOfSpeechSection.PossibleSuru) &&
                    nextWord.DictionaryForm == "する" && nextWord.Text != "する" && nextWord.Text != "しない")
                {
                    WordInfo combinedWord = new WordInfo(currentWord);
                    combinedWord.Text += nextWord.Text;
                    // combinedWord.PartOfSpeech = PartOfSpeech.Verb;
                    newList.Add(combinedWord);
                    i += 2;
                    continue;
                }
            }

            newList.Add(new WordInfo(currentWord));
            i++;
        }

        return newList;
    }

    private List<WordInfo> CombineVerbDependantsTeiru(List<WordInfo> wordInfos)
    {
        if (wordInfos.Count < 2)
            return wordInfos;

        List<WordInfo> newList = new List<WordInfo>();
        int i = 0;
        while (i < wordInfos.Count)
        {
            WordInfo currentWord = wordInfos[i];

            // Pattern 1: Verb + て (particle) + いる (3 tokens)
            if (i + 2 < wordInfos.Count)
            {
                WordInfo nextWord1 = wordInfos[i + 1];
                WordInfo nextWord2 = wordInfos[i + 2];

                if (currentWord.PartOfSpeech is PartOfSpeech.Verb &&
                    nextWord1.DictionaryForm == "て" &&
                    nextWord2.DictionaryForm == "いる")
                {
                    WordInfo combinedWord = new WordInfo(currentWord);
                    combinedWord.Text += nextWord1.Text + nextWord2.Text;
                    newList.Add(combinedWord);
                    i += 3;
                    continue;
                }
            }

            // Pattern 2: Word ending in て/で + subsidiary verb (2 tokens)
            // Handles cases where て is already combined with the verb/adjective (e.g., うらやましがられて + いる, 進んで + ない, 愛して + あげられる)
            if (i + 1 < wordInfos.Count)
            {
                WordInfo nextWord = wordInfos[i + 1];

                if ((currentWord.Text.EndsWith("て") || currentWord.Text.EndsWith("で")) &&
                    currentWord.PartOfSpeech is PartOfSpeech.Verb or PartOfSpeech.IAdjective &&
                    nextWord.PartOfSpeech == PartOfSpeech.Verb &&
                    nextWord.DictionaryForm != "おる")
                {
                    bool isKnownSubsidiary =
                        (nextWord.HasPartOfSpeechSection(PartOfSpeechSection.PossibleDependant) &&
                         nextWord.DictionaryForm is "いる" or "ない") ||
                        TeFormSubsidiaryVerbs.Contains(nextWord.DictionaryForm) ||
                        TeFormSubsidiaryVerbs.Contains(nextWord.NormalizedForm);

                    // Handle conjugated subsidiary verbs (e.g., あげられる = potential/passive of あげる)
                    // Sudachi may tag these as standalone verbs rather than subsidiary forms
                    if (!isKnownSubsidiary)
                    {
                        var deconj = Deconjugator.Instance;
                        string nextHiragana = KanaNormalizer.Normalize(WanaKana.ToHiragana(nextWord.Text));
                        var forms = deconj.Deconjugate(nextHiragana);
                        isKnownSubsidiary = forms.Any(f => TeFormSubsidiaryVerbs.Contains(f.Text));
                    }

                    if (isKnownSubsidiary)
                    {
                        WordInfo combinedWord = new WordInfo(currentWord);
                        combinedWord.Text += nextWord.Text;
                        newList.Add(combinedWord);
                        i += 2;
                        continue;
                    }
                }
            }

            newList.Add(new WordInfo(currentWord));
            i++;
        }

        return newList;
    }

}
