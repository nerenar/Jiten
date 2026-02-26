using FluentAssertions;
using Jiten.Core.Data.JMDict;
using Jiten.Parser;
using Jiten.Parser.Diagnostics;
using Jiten.Parser.Scoring;
using Xunit;

namespace Jiten.Tests;

public class FormScoringTests
{
    [Fact]
    public void Score_ShouldBeDeterministic_ForSameCandidate()
    {
        var candidate = CreateCandidate(
            wordId: 1001,
            surfaceFormText: "テスト",
            formType: JmDictFormType.KanaForm,
            wordPriorities: ["ichi1"],
            formPriorities: ["news1"]);

        var context = FormScoringContext.Create(
            surface: "テスト",
            dictionaryForm: "テスト",
            normalizedForm: "テスト",
            isNameContext: false,
            sudachiReading: null);

        var first = FormCandidateScorer.Score(candidate, context, new HashSet<string>());
        var second = FormCandidateScorer.Score(candidate, context, new HashSet<string>());

        first.Should().Be(second);
    }

    [Fact]
    public void Selector_ShouldBreakTie_ByLowerWordId()
    {
        var lowId = CreateCandidate(100, "かな", JmDictFormType.KanaForm);
        var highId = CreateCandidate(200, "かな", JmDictFormType.KanaForm);

        var context = FormScoringContext.Create(
            surface: "かな",
            dictionaryForm: null,
            normalizedForm: null,
            isNameContext: false,
            sudachiReading: null);

        var best = FormCandidateSelector.PickBestCandidate(
            [highId, lowId],
            context,
            new HashSet<string>(),
            diagnostics: null);

        best.Should().NotBeNull();
        best!.Word.WordId.Should().Be(100);
    }

    [Fact]
    public void Selector_ShouldEmitDiagnostics_FromScoreTrace()
    {
        var candidate = CreateCandidate(1234, "テスト", JmDictFormType.KanaForm);
        var diagnostics = new ParserDiagnostics();

        var context = FormScoringContext.Create(
            surface: "テスト",
            dictionaryForm: "テスト",
            normalizedForm: null,
            isNameContext: false,
            sudachiReading: "テスト");

        var best = FormCandidateSelector.PickBestCandidate(
            [candidate],
            context,
            new HashSet<string>(),
            diagnostics);

        best.Should().NotBeNull();
        diagnostics.Results.Should().ContainSingle();

        var result = diagnostics.Results.Single();
        result.WordId.Should().Be(1234);
        result.Candidates.Should().ContainSingle();

        var detail = result.Candidates.Single();
        detail.TotalScore.Should().Be(candidate.TotalScore);
        detail.WordScore.Should().Be(candidate.WordScore);
        detail.EntryPriorityScore.Should().Be(candidate.EntryPriorityScore);
        detail.FormPriorityScore.Should().Be(candidate.FormPriorityScore);
        detail.FormFlagScore.Should().Be(candidate.FormFlagScore);
        detail.SurfaceMatchScore.Should().Be(candidate.SurfaceMatchScore);
        detail.ScriptScore.Should().Be(candidate.ScriptScore);
        detail.ReadingMatchScore.Should().Be(candidate.ReadingMatchScore);
    }

    [Fact]
    public void Score_ShouldZeroReading_WhenConjugatedIdentityPenaltyApplies()
    {
        var kanjiForm = new JmDictWordForm
        {
            WordId = 3001,
            ReadingIndex = 0,
            Text = "飛んで",
            FormType = JmDictFormType.KanjiForm,
            IsActiveInLatestSource = true
        };

        var kanaForm = new JmDictWordForm
        {
            WordId = 3001,
            ReadingIndex = 1,
            Text = "とんで",
            FormType = JmDictFormType.KanaForm,
            IsActiveInLatestSource = true
        };

        var word = new JmDictWord
        {
            WordId = 3001,
            PartsOfSpeech = ["v5b"],
            Priorities = [],
            Forms = [kanjiForm, kanaForm]
        };

        var candidate = new FormCandidate(word, kanjiForm, 0, "とんで");

        var penalizedContext = FormScoringContext.Create(
            surface: "飛んで",
            dictionaryForm: "飛ぶ",
            normalizedForm: null,
            isNameContext: false,
            sudachiReading: "トンデ");

        var nonPenalizedContext = penalizedContext with { DictionaryForm = "飛んで" };

        var penalized = FormCandidateScorer.Score(candidate, penalizedContext, new HashSet<string>());
        var nonPenalized = FormCandidateScorer.Score(candidate, nonPenalizedContext, new HashSet<string>());

        penalized.IdentityPenaltyApplied.Should().BeTrue();
        penalized.ReadingMatchScore.Should().Be(0);
        nonPenalized.ReadingMatchScore.Should().BeGreaterThan(0);
    }

    private static FormCandidate CreateCandidate(
        int wordId,
        string surfaceFormText,
        JmDictFormType formType,
        List<string>? wordPriorities = null,
        List<string>? formPriorities = null,
        List<string>? partsOfSpeech = null)
    {
        var form = new JmDictWordForm
        {
            WordId = wordId,
            ReadingIndex = 0,
            Text = surfaceFormText,
            FormType = formType,
            Priorities = formPriorities,
            IsActiveInLatestSource = true
        };

        var word = new JmDictWord
        {
            WordId = wordId,
            PartsOfSpeech = partsOfSpeech ?? [],
            Priorities = wordPriorities ?? [],
            Forms = [form]
        };

        return new FormCandidate(word, form, 0, surfaceFormText);
    }
}
