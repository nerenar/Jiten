using Jiten.Core.Data.JMDict;
using WanaKanaShaapu;

namespace Jiten.Parser.Scoring;

internal sealed class FormCandidate(
    JmDictWord word,
    JmDictWordForm form,
    byte readingIndex,
    string targetHiragana,
    DeconjugationForm? deconjForm = null)
{
    public JmDictWord Word { get; } = word;
    public JmDictWordForm Form { get; } = form;
    public byte ReadingIndex { get; } = readingIndex;
    public string TargetHiragana { get; } = targetHiragana;
    public DeconjugationForm? DeconjForm { get; } = deconjForm;

    public FormScoreTrace ScoreTrace { get; private set; }

    public int TotalScore => ScoreTrace.TotalScore;
    public int WordScore => ScoreTrace.WordScore;
    public int EntryPriorityScore => ScoreTrace.EntryPriorityScore;
    public int FormPriorityScore => ScoreTrace.FormPriorityScore;
    public int FormFlagScore => ScoreTrace.FormFlagScore;
    public int SurfaceMatchScore => ScoreTrace.SurfaceMatchScore;
    public int ScriptScore => ScoreTrace.ScriptScore;
    public int ReadingMatchScore => ScoreTrace.ReadingMatchScore;

    public void SetScoreTrace(FormScoreTrace scoreTrace) => ScoreTrace = scoreTrace;
}

internal readonly record struct FormScoringContext(
    string Surface,
    string? DictionaryForm,
    string? NormalizedForm,
    bool IsNameContext,
    string? SudachiReading,
    bool IsKanaSurface)
{
    public static FormScoringContext Create(
        string surface,
        string? dictionaryForm,
        string? normalizedForm,
        bool isNameContext,
        string? sudachiReading)
    {
        return new FormScoringContext(
            surface,
            dictionaryForm,
            normalizedForm,
            isNameContext,
            sudachiReading,
            WanaKana.IsKana(surface));
    }
}

internal readonly record struct FormScoreTrace(
    int WordScore,
    int EntryPriorityScore,
    int FormPriorityScore,
    int FormFlagScore,
    int SurfaceMatchScore,
    int ScriptScore,
    int ReadingMatchScore,
    bool ConjugatedIdentityPenaltyApplied)
{
    public int TotalScore =>
        WordScore + EntryPriorityScore + FormPriorityScore + FormFlagScore + SurfaceMatchScore + ScriptScore +
        ReadingMatchScore;
}
