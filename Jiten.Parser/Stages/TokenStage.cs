namespace Jiten.Parser;

internal enum TokenStageGroup
{
    Split,
    Repair,
    Combine,
    Cleanup,
    Disambiguation
}

internal sealed class TokenStage(
    string name,
    TokenStageGroup group,
    Func<List<WordInfo>, List<WordInfo>> process)
{
    public string Name { get; } = name;
    public TokenStageGroup Group { get; } = group;
    public List<WordInfo> Apply(List<WordInfo> input) => process(input);
}
