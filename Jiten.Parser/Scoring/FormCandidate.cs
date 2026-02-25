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
    public bool IsPosIncompatibleDirectSurface { get; set; }

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
    bool IsKanaSurface,
    bool IsArchaicSentence = false,
    bool IsSentenceInitial = false)
{
    public static FormScoringContext Create(
        string surface,
        string? dictionaryForm,
        string? normalizedForm,
        bool isNameContext,
        string? sudachiReading,
        bool isArchaicSentence = false,
        bool isSentenceInitial = false)
    {
        return new FormScoringContext(
            surface,
            dictionaryForm,
            normalizedForm,
            isNameContext,
            sudachiReading,
            WanaKana.IsKana(surface),
            isArchaicSentence,
            isSentenceInitial);
    }
}

internal sealed record CandidateSelectionResult(
    FormCandidate? Best,
    IReadOnlyList<FormCandidate> TopN,
    int? MarginToSecond)
{
    public bool IsLowConfidence    => MarginToSecond.HasValue && MarginToSecond.Value < ScoringPolicy.LowConfidenceThreshold;
    public bool IsMediumConfidence => MarginToSecond.HasValue && MarginToSecond.Value is >= ScoringPolicy.LowConfidenceThreshold and < ScoringPolicy.HighConfidenceThreshold;
    public bool IsHighConfidence   => MarginToSecond.HasValue && MarginToSecond.Value >= ScoringPolicy.HighConfidenceThreshold;
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
