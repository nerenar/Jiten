using Jiten.Core.Data;
using Jiten.Core.Utils;
using WanaKanaShaapu;

namespace Jiten.Parser;

public partial class MorphologicalAnalyser
{
    private bool CompoundExistsInLookup(string compoundForm, Func<string, IReadOnlyList<DeconjugationForm>> cachedDeconjugate)
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

    private static readonly HashSet<char> DictionaryVerbEndings =
        ['う', 'く', 'ぐ', 'す', 'つ', 'ぬ', 'ぶ', 'む', 'る'];

    internal static string? TryGodanDictForm(string text)
    {
        if (text.Length < 2) return null;
        var dictEnding = text[^1] switch
        {
            'い' => 'う', 'き' => 'く', 'ぎ' => 'ぐ', 'し' => 'す',
            'ち' => 'つ', 'に' => 'ぬ', 'び' => 'ぶ', 'み' => 'む', 'り' => 'る',
            _ => '\0'
        };
        return dictEnding == '\0' ? null : text[..^1] + dictEnding;
    }

    private bool VerbDictFormExistsInLookup(string dictForm, string? normalizedForm, Func<string, IReadOnlyList<DeconjugationForm>> cachedDeconjugate)
    {
        if (HasCompoundLookup!(dictForm))
            return true;

        if (normalizedForm != null && normalizedForm != dictForm && HasCompoundLookup(normalizedForm))
            return true;

        foreach (var form in cachedDeconjugate(dictForm))
        {
            if (form.Text.Length > 0 && DictionaryVerbEndings.Contains(form.Text[^1]) &&
                HasCompoundLookup(form.Text))
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
                currentWord.EndOffset = nextWord.EndOffset;
                currentWord.Reading += nextWord.Reading;
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
            bool isClassicalWaRowTeForm = nextWord.DictionaryForm.EndsWith("う") &&
                                          nextWord.Text.EndsWith("いて");
            if (nextWord.HasPartOfSpeechSection(PartOfSpeechSection.PossibleDependant) &&
                currentWord.PartOfSpeech == PartOfSpeech.Verb && !currentWord.Text.EndsWith("たり") &&
                nextWord.Text != currentWord.Text &&
                !isClassicalWaRowTeForm &&
                (nextWord.DictionaryForm is "得る" or "しまう" or "こなす" or "いく" or "貰う" or "いる" or "ない" or "だす" ||
                 (nextWord.DictionaryForm == "する" && (currentWord.Text.EndsWith("た") || currentWord.Text.EndsWith("だ")))))
            {
                currentWord.Text += nextWord.Text;
                currentWord.EndOffset = nextWord.EndOffset;
                currentWord.Reading += nextWord.Reading;
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
                    combinedWord.EndOffset = nextWord.EndOffset;
                    combinedWord.Reading += nextWord.Reading;
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

            // Pattern 1: Verb + て (particle) + te-form auxiliary (3 tokens)
            // Handles cases where て wasn't merged by CombineConjunctiveParticle
            if (i + 2 < wordInfos.Count)
            {
                WordInfo nextWord1 = wordInfos[i + 1];
                WordInfo nextWord2 = wordInfos[i + 2];

                if (currentWord.PartOfSpeech is PartOfSpeech.Verb &&
                    nextWord1.DictionaryForm == "て" &&
                    TeFormAuxChainVerbs.Contains(nextWord2.DictionaryForm))
                {
                    WordInfo combinedWord = new WordInfo(currentWord);
                    combinedWord.Text += nextWord1.Text + nextWord2.Text;
                    combinedWord.EndOffset = nextWord2.EndOffset;
                    combinedWord.Reading += nextWord1.Reading + nextWord2.Reading;
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

                // Classical ワ行 te-form: e.g. 貰いて (DictForm=貰う + いて suffix) vs modern 貰って
                // These shouldn't be merged as subsidiary verbs; they're their own te-form tokens
                bool isClassicalWaRowTeForm = nextWord.DictionaryForm.EndsWith("う") &&
                                              nextWord.Text.EndsWith("いて");
                if ((currentWord.Text.EndsWith("て") || currentWord.Text.EndsWith("で")) &&
                    currentWord.PartOfSpeech is PartOfSpeech.Verb or PartOfSpeech.IAdjective &&
                    nextWord.PartOfSpeech == PartOfSpeech.Verb &&
                    nextWord.DictionaryForm != "おる" &&
                    !isClassicalWaRowTeForm)
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
                        string nextHiragana = KanaNormalizer.Normalize(KanaConverter.ToHiragana(nextWord.Text));
                        var forms = deconj.Deconjugate(nextHiragana);
                        isKnownSubsidiary = forms.Any(f => TeFormSubsidiaryVerbs.Contains(f.Text));
                    }

                    if (isKnownSubsidiary)
                    {
                        WordInfo combinedWord = new WordInfo(currentWord);
                        combinedWord.Text += nextWord.Text;
                        combinedWord.EndOffset = nextWord.EndOffset;
                        combinedWord.Reading += nextWord.Reading;
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
