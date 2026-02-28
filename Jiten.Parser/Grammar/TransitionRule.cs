using Jiten.Core.Data;
using Jiten.Parser;
using Jiten.Parser.Scoring;

namespace Jiten.Parser.Grammar;

internal enum RuleSeverity { Hard, Soft }

internal enum ViolationAction
{
    None,
    RemoveCurrent,
    MergeWithPrevious,
    ReclassifyCurrentAsNoun,
    RequestResegmentation
}

internal enum MatchCondition
{
    IsVerbOnlyAux,       // Auxiliary with DictionaryForm in VerbOnlyAuxDictForms
    IsVerbOrAdjAux,      // Auxiliary with DictionaryForm in VerbOrAdjAuxDictForms
    IsVerbAttachingAux,  // IsVerbOnlyAux || IsVerbOrAdjAux (for leading-strip rule)
    IsAuxiliary,         // PartOfSpeech == Auxiliary
    IsCounter,           // PartOfSpeech == Counter || (Suffix with Counter section)
    IsSentenceInitial,   // Index == 0
    PrevIsVerbOrAux,             // Prev.PartOfSpeech is Verb or Auxiliary
    PrevIsVerbAuxOrIAdj,         // Prev.PartOfSpeech is Verb, Auxiliary, or IAdjective
    PrevIsVerbAuxIAdjOrSfp,      // Prev.PartOfSpeech is Verb, Auxiliary, IAdjective, or sentence-ending particle
    PrevIsAuxiliary,             // Prev.PartOfSpeech is Auxiliary
    PrevIsAuxiliaryOrParticle,   // Prev.PartOfSpeech is Auxiliary or Particle
    PrevIsNumericOrNoun, // Prev.PartOfSpeech is Numeral, Noun, CommonNoun, Pronoun, or Name
    PrevExists,          // Prev != null
    IsSentenceEndingParticle, // PartOfSpeech == Particle && Section == SentenceEndingParticle
    NextIsContentWord,   // Next.PartOfSpeech is a content-bearing POS (noun/verb/adj/adverb/etc.)
    IsPrefix,            // PartOfSpeech == Prefix
    IsSentenceFinal,     // Index == Count - 1
    NextIsParticle,      // Next.PartOfSpeech == Particle
    IsSuffix,            // PartOfSpeech == Suffix
    IsStrictCaseMarkingParticle // Particle with DictionaryForm in StrictCaseMarkingParticles (が/を/へ)
}

internal sealed record TransitionRule(
    string Id,
    RuleSeverity Severity,
    MatchCondition[] WhenToken,
    MatchCondition[] ValidIf,
    ViolationAction OnViolation,
    int SoftDelta = 0);

internal readonly record struct TokenWindow(
    WordInfo? Prev,
    WordInfo Current,
    WordInfo? Next,
    int Index,
    int Count);

internal enum ScoringCondition
{
    CandidateIsNounLike,
    CandidateIsNaAdj,
    CandidateIsAdverb,
    CandidateIsAuxiliary,
    CandidateIsParticle,
    CandidateIsSingleKanaNonParticle,

    NextIsCommonParticle,
    NextIsCopula,
    NextIsNaConnector,
    NextIsVerbOrIAdj,
    PrevIsVerbOrIAdj,
    PrevIsParticle,
    NextIsParticle,
    PrevIsSingleKanaNonParticle,
    NextIsSingleKanaNonParticle,
    CandidateIsPredicateHost,
    CandidateIsNoParticle,
    NextIsExplanatoryN,
    PrevIsVerbAuxOrIAdj,
    CandidateIsCounter,
    PrevIsNumeral,
    PrevIsNotNumericLike,
    CandidateIsSingleKanji,
    PrevIsSingleKanji,
    NextIsSingleKanji,
    NextIsConditionalParticle,
    CandidateIsAdvTo,
    NextIsToParticle,
    CandidateIsVerb,
    NextIsTeFormAux,
    PrevIsNoParticle,
    NextIsNotNaAdjConnector,
    NextIsBaParticle,
    CandidateIsPrenounAdjectival,
    NextIsNounLike,
    CandidateIsConjunction,
    IsSentenceInitial,
    CandidateIsInterjection,
    CandidateIsSuruNoun,
    NextIsSuru,
    PrevIsCaseParticle,
    CandidateIsName,
    NextIsHonorific,
    IsSentenceFinal,
    CandidateIsNounSuffix,
    PrevIsNounLike,
    CandidateIsNotNounLike,
    NextIsNotNounLike,
    CandidateIsHonorific,
    PrevIsName,
}

internal sealed record ScoringRule(
    string Id,
    ScoringCondition[] CandidateMatch,
    ScoringCondition[] ContextMatch,
    int Delta);

internal readonly record struct ScoringWindow(
    FormCandidate Candidate,
    List<PartOfSpeech>? PrevResolvedPOS,
    List<PartOfSpeech>? NextResolvedPOS,
    string? PrevText,
    string? NextText);
