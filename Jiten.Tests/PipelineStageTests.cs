using FluentAssertions;
using Jiten.Core.Data;
using Jiten.Parser;
using Jiten.Parser.Diagnostics;

namespace Jiten.Tests;

public class PipelineStageTests
{
    private static WordInfo Token(
        string text,
        PartOfSpeech partOfSpeech,
        string? dictionaryForm = null,
        string? normalizedForm = null,
        string? reading = null,
        PartOfSpeechSection section1 = PartOfSpeechSection.None,
        PartOfSpeechSection section2 = PartOfSpeechSection.None,
        PartOfSpeechSection section3 = PartOfSpeechSection.None) =>
        new()
        {
            Text = text,
            PartOfSpeech = partOfSpeech,
            DictionaryForm = dictionaryForm ?? text,
            NormalizedForm = normalizedForm ?? dictionaryForm ?? text,
            Reading = reading ?? text,
            PartOfSpeechSection1 = section1,
            PartOfSpeechSection2 = section2,
            PartOfSpeechSection3 = section3
        };

    [Fact]
    public void PipelineOrder_ShouldRemainStable()
    {
        var analyser = new MorphologicalAnalyser();

        var names = analyser.GetPipelineStageNamesForTesting();

        names.Should().Equal(
            "SplitCompoundAuxiliaryVerbs",
            "SplitTatteParticle",
            "RepairNTokenisation",
            "RepairVowelElongation",
            "ProcessSpecialCases",
            "CombinePrefixes",
            "CombineInflections",
            "CombineAmounts",
            "CombineTte",
            "CombineAuxiliaryVerbStem",
            "CombineSuffix",
            "ReclassifyOrphanedSuffixes",
            "CombineConjunctiveParticle",
            "CombineAuxiliary",
            "RepairOrphanedAuxiliary",
            "CombineAdverbialParticle",
            "CombineVerbDependant",
            "CombineParticles",
            "CombineFinal",
            "RepairTankaToTaNKa",
            "FilterMisparse",
            "FixReadingAmbiguity");
    }

    [Fact]
    public void SplitStage_ShouldSplitTatteParticle()
    {
        var analyser = new MorphologicalAnalyser();
        var input = new List<WordInfo>
        {
            Token("出", PartOfSpeech.Verb, dictionaryForm: "出る", normalizedForm: "出る", reading: "で"),
            Token("たって", PartOfSpeech.Particle, section1: PartOfSpeechSection.ConjunctionParticle)
        };

        var output = analyser.ApplyStageForTesting("SplitTatteParticle", input);

        output.Select(t => t.Text).Should().Equal("出", "た", "って");
        output[1].PartOfSpeech.Should().Be(PartOfSpeech.Auxiliary);
        output[2].PartOfSpeech.Should().Be(PartOfSpeech.Particle);
    }

    [Fact]
    public void RepairStage_ShouldRecoverNdaPastTense_FromCorpusCase()
    {
        var analyser = new MorphologicalAnalyser();
        var input = new List<WordInfo>
        {
            Token("読ん", PartOfSpeech.Verb, dictionaryForm: "読む", normalizedForm: "読む", reading: "よん"),
            Token("だけど", PartOfSpeech.Conjunction, dictionaryForm: "だけど", normalizedForm: "だけど", reading: "だけど")
        };

        var output = analyser.ApplyStageForTesting("RepairNTokenisation", input);

        output.Select(t => t.Text).Should().Equal("読んだ", "けど");
        output[0].PartOfSpeech.Should().Be(PartOfSpeech.Verb);
    }

    [Fact]
    public void CombineStage_ShouldCombineParticlePairs()
    {
        var analyser = new MorphologicalAnalyser();
        var input = new List<WordInfo>
        {
            Token("に", PartOfSpeech.Particle),
            Token("は", PartOfSpeech.Particle)
        };

        var output = analyser.ApplyStageForTesting("CombineParticles", input);

        output.Select(t => t.Text).Should().Equal("には");
    }

    [Fact]
    public void DisambiguationStage_ShouldAdjustReadingByContext()
    {
        var analyser = new MorphologicalAnalyser();
        var input = new List<WordInfo>
        {
            Token("表", PartOfSpeech.Noun, reading: "ヒョウ"),
            Token("へ", PartOfSpeech.Particle, reading: "へ")
        };

        var output = analyser.ApplyStageForTesting("FixReadingAmbiguity", input);

        output[0].Reading.Should().Be("オモテ");
    }

    [Fact]
    public void StageDiagnostics_ShouldRecordStageInfo()
    {
        var analyser = new MorphologicalAnalyser();
        var diagnostics = new ParserDiagnostics();
        var input = new List<WordInfo>
        {
            Token("に", PartOfSpeech.Particle),
            Token("は", PartOfSpeech.Particle)
        };

        analyser.ApplyStageForTesting("CombineParticles", input, diagnostics);

        diagnostics.TokenStages.Should().HaveCount(1);
        var stage = diagnostics.TokenStages[0];
        stage.StageName.Should().Be("CombineParticles");
        stage.StageGroup.Should().Be("Combine");
    }
}
