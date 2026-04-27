using Jiten.Core.Data;
using Jiten.Core.Data.JMDict;
using WanaKanaShaapu;

namespace Jiten.Parser.Misparse;

internal readonly record struct MisparseDecision(bool IsMisparsed, string? GateId = null);

internal readonly record struct MisparseGateContext(
    WordInfo Token,
    DeckWord SelectedWord,
    WordInfo? Prev,
    WordInfo? Next,
    bool IsUsuallyKana,
    bool HasKanjiSpelling,
    bool ReadingIsIchi);

internal static class MisparseGates
{
    private static readonly HashSet<PartOfSpeech> ExemptFromKanaGate =
    [
        PartOfSpeech.Particle, PartOfSpeech.Auxiliary, PartOfSpeech.Conjunction,
        PartOfSpeech.Adnominal
    ];

    public static MisparseDecision Evaluate(in MisparseGateContext ctx)
    {
        if (IsShortKanaNameWithoutContext(in ctx))
            return new(true, "short-kana-name");

        if (IsShortKanaTokenWithoutJustification(in ctx))
            return new(true, "short-kana-unjustified");

        return default;
    }

    private static bool IsShortKanaNameWithoutContext(in MisparseGateContext ctx)
    {
        if (!WanaKana.IsKana(ctx.Token.Text)) return false;
        if (ctx.Token.Text.Length > 2) return false;
        if (!ctx.SelectedWord.PartsOfSpeech.Contains(PartOfSpeech.Name)) return false;
        if (ctx.Token.IsPersonNameContext) return false;

        return true;
    }

    private static bool IsShortKanaTokenWithoutJustification(in MisparseGateContext ctx)
    {
        string surface = ctx.Token.Text;

        if (!WanaKana.IsKana(surface)) return false;
        if (surface.Length > 2) return false;

        if (ExemptFromKanaGate.Contains(ctx.Token.PartOfSpeech)) return false;

        if (ctx.IsUsuallyKana) return false;

        if (!ctx.HasKanjiSpelling) return false;

        // Kana reading is in basic vocabulary (ichi1) — too common to reject
        if (ctx.ReadingIsIchi) return false;

        if (ctx.Next != null && IsGrammaticalFollower(ctx.Next.Text))
            return false;

        return true;
    }

    private static bool IsGrammaticalFollower(string text)
        => text is "が" or "を" or "に" or "は" or "の" or "で" or "と" or "へ"
           or "から" or "まで" or "より" or "も" or "って" or "だ" or "です";

    public static (bool isUsuallyKana, bool hasKanjiSpelling, bool readingIsIchi) GetWordFlags(
        JmDictWord? word, byte readingIndex)
    {
        if (word == null) return (false, true, false);

        bool isUk = word.PartsOfSpeech.Contains("uk");
        bool hasKanji = word.Forms.Any(f => f.FormType == JmDictFormType.KanjiForm);

        bool readingIsIchi = word.Priorities?.Contains("jiten") == true
                             || word.Forms.Any(f => f.FormType == JmDictFormType.KanaForm
                                                    && f.ReadingIndex == readingIndex
                                                    && f.Priorities != null
                                                    && (f.Priorities.Contains("ichi1") || f.Priorities.Contains("ichi2")));

        return (isUk, hasKanji, readingIsIchi);
    }
}
