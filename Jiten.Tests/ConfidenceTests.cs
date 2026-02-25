using FluentAssertions;
using Jiten.Core.Data.JMDict;
using Jiten.Parser.Diagnostics;
using Jiten.Parser.Scoring;
using Xunit;

namespace Jiten.Tests;

public class ConfidenceTests
{
    [Fact]
    public void SingleCandidate_MarginIsNull_LevelIsSingle()
    {
        var candidate = CreateCandidate(1, "テスト");
        var context = MakeContext("テスト");

        var result = FormCandidateSelector.PickTopCandidates([candidate], context, new HashSet<string>());

        result.Best.Should().NotBeNull();
        result.MarginToSecond.Should().BeNull();
        result.Best!.Word.WordId.Should().Be(1);

        var wordResult = CaptureWordResult([candidate], context);
        wordResult.MarginToSecond.Should().BeNull();
        wordResult.ConfidenceLevel.Should().Be("single");
    }

    [Fact]
    public void TwoCandidates_LargeGap_IsHighConfidence()
    {
        var result = new CandidateSelectionResult(null, [], 40);
        result.IsHighConfidence.Should().BeTrue();
        result.IsMediumConfidence.Should().BeFalse();
        result.IsLowConfidence.Should().BeFalse();
    }

    [Fact]
    public void TwoCandidates_PriorityDiff_ProducesNonNullMargin()
    {
        var high = CreateCandidateWithPriority(1, "テスト", ["ichi1"], ["ichi1"]);
        var low  = CreateCandidate(2, "テスト");
        var context = MakeContext("テスト");

        var result = FormCandidateSelector.PickTopCandidates([high, low], context, new HashSet<string>());

        result.MarginToSecond.Should().NotBeNull();
        result.MarginToSecond.Should().BeGreaterThan(0);
        result.Best!.Word.WordId.Should().Be(1);
    }

    [Fact]
    public void TwoCandidates_MediumGap_IsMediumConfidence()
    {
        var result = new CandidateSelectionResult(null, [], 25);
        result.IsMediumConfidence.Should().BeTrue();
        result.IsHighConfidence.Should().BeFalse();
        result.IsLowConfidence.Should().BeFalse();
    }

    [Fact]
    public void TwoCandidates_SmallGap_IsLowConfidence()
    {
        var result = new CandidateSelectionResult(null, [], 10);
        result.IsLowConfidence.Should().BeTrue();
        result.IsMediumConfidence.Should().BeFalse();
        result.IsHighConfidence.Should().BeFalse();
    }

    [Fact]
    public void ZeroCandidates_BestIsNull_MarginIsNull()
    {
        var context = MakeContext("テスト");

        var result = FormCandidateSelector.PickTopCandidates([], context, new HashSet<string>());

        result.Best.Should().BeNull();
        result.MarginToSecond.Should().BeNull();
        result.TopN.Should().BeEmpty();
    }

    [Fact]
    public void Tiebreak_MarginIsZero_IsLowConfidence()
    {
        var a = CreateCandidate(100, "かな");
        var b = CreateCandidate(200, "かな");
        var context = MakeContext("かな");

        var result = FormCandidateSelector.PickTopCandidates([a, b], context, new HashSet<string>());

        result.MarginToSecond.Should().Be(0);
        result.IsLowConfidence.Should().BeTrue();
        result.Best!.Word.WordId.Should().Be(100, "lower WordId wins tiebreak");
    }

    [Fact]
    public void PickTopCandidates_WinnerMatchesPickBestCandidate()
    {
        var high = CreateCandidateWithPriority(1, "テスト", ["ichi1"], ["ichi1"]);
        var low  = CreateCandidate(2, "テスト");
        var context = MakeContext("テスト");

        var topResult  = FormCandidateSelector.PickTopCandidates([high, low], context, new HashSet<string>());
        var bestResult = FormCandidateSelector.PickBestCandidate([high, low], context, new HashSet<string>());

        topResult.Best.Should().NotBeNull();
        bestResult.Should().NotBeNull();
        topResult.Best!.Word.WordId.Should().Be(bestResult!.Word.WordId);
        topResult.Best.ReadingIndex.Should().Be(bestResult.ReadingIndex);
    }

    private static WordResult CaptureWordResult(List<FormCandidate> candidates, FormScoringContext context)
    {
        var diagnostics = new ParserDiagnostics();
        FormCandidateSelector.PickTopCandidates(candidates, context, new HashSet<string>(), diagnostics);
        return diagnostics.Results.Should().ContainSingle().Subject;
    }

    private static FormScoringContext MakeContext(string surface) =>
        FormScoringContext.Create(surface, surface, null, false, null);

    private static FormCandidate CreateCandidate(int wordId, string text) =>
        CreateCandidateWithPriority(wordId, text, null, null);

    private static FormCandidate CreateCandidateWithPriority(
        int wordId, string text,
        List<string>? wordPriorities,
        List<string>? formPriorities)
    {
        var form = new JmDictWordForm
        {
            WordId = wordId,
            ReadingIndex = 0,
            Text = text,
            FormType = JmDictFormType.KanaForm,
            Priorities = formPriorities,
            IsActiveInLatestSource = true
        };
        var word = new JmDictWord
        {
            WordId = wordId,
            PartsOfSpeech = [],
            Priorities = wordPriorities ?? [],
            Forms = [form]
        };
        return new FormCandidate(word, form, 0, text);
    }
}
