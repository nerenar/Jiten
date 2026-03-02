using Jiten.Parser.Diagnostics;

namespace Jiten.Parser;

public partial class MorphologicalAnalyser
{
    private IReadOnlyList<TokenStage>? _tokenStages;

    private IReadOnlyList<TokenStage> GetTokenStages() => _tokenStages ??= BuildTokenStages();

    private static TokenStage Stage(TokenStageGroup group, Func<List<WordInfo>, List<WordInfo>> process) =>
        new(process.Method.Name, group, process);

    private IReadOnlyList<TokenStage> BuildTokenStages() =>
    [
        Stage(TokenStageGroup.Split, SplitCompoundAuxiliaryVerbs),
        Stage(TokenStageGroup.Split, SplitTatteParticle),
        Stage(TokenStageGroup.Split, SplitTanSuffix),
        Stage(TokenStageGroup.Repair, RepairHasaNoun),
        Stage(TokenStageGroup.Repair, RepairNTokenisation),
        Stage(TokenStageGroup.Repair, RepairVowelElongation),
        Stage(TokenStageGroup.Repair, ProcessSpecialCases),
        Stage(TokenStageGroup.Repair, RepairColloquialNegativeNee),
        Stage(TokenStageGroup.Combine, CombinePrefixes),
        Stage(TokenStageGroup.Combine, CombineInflections),
        Stage(TokenStageGroup.Combine, CombineAmounts),
        Stage(TokenStageGroup.Combine, CombineTte),
        Stage(TokenStageGroup.Combine, CombineAuxiliaryVerbStem),
        Stage(TokenStageGroup.Combine, CombineSuffix),
        Stage(TokenStageGroup.Cleanup, ReclassifyOrphanedSuffixes),
        Stage(TokenStageGroup.Combine, CombineConjunctiveParticle),
        Stage(TokenStageGroup.Combine, CombineAuxiliary),
        Stage(TokenStageGroup.Combine, CombineToNaru),
        Stage(TokenStageGroup.Repair, RepairFusedInterjectionParticle),
        Stage(TokenStageGroup.Repair, RepairOrphanedAuxiliary),
        Stage(TokenStageGroup.Combine, CombineAdverbialParticle),
        Stage(TokenStageGroup.Combine, CombineVerbDependant),
        Stage(TokenStageGroup.Combine, CombineParticles),
        Stage(TokenStageGroup.Combine, CombineFinal),
        Stage(TokenStageGroup.Repair, RepairTankaToTaNKa),
        Stage(TokenStageGroup.Cleanup, FilterMisparse),
        Stage(TokenStageGroup.Disambiguation, FixReadingAmbiguity),
    ];

    private List<WordInfo> RunPipeline(List<WordInfo> wordInfos, ParserDiagnostics? diagnostics)
    {
        foreach (var stage in GetTokenStages())
            wordInfos = TrackStage(stage, wordInfos, diagnostics);

        return wordInfos;
    }

    internal IReadOnlyList<string> GetPipelineStageNamesForTesting() =>
        GetTokenStages().Select(s => s.Name).ToList();

    internal List<WordInfo> ApplyStageForTesting(string stageName, List<WordInfo> input, ParserDiagnostics? diagnostics = null)
    {
        var stage = GetTokenStages().First(s => s.Name == stageName);
        return TrackStage(stage, input, diagnostics);
    }

    internal List<WordInfo> RunPipelineForTesting(List<WordInfo> input, ParserDiagnostics? diagnostics = null)
    {
        var output = input;
        foreach (var stage in GetTokenStages())
            output = TrackStage(stage, output, diagnostics);

        return output;
    }
}
