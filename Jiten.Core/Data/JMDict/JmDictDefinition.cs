namespace Jiten.Core.Data.JMDict;

public class JmDictDefinition
{
    public int DefinitionId { get; init; }
    public int WordId { get; init; }
    public List<string> PartsOfSpeech { get; init; } = new();
    public List<string> EnglishMeanings { get; init; } = new();
    public List<string> DutchMeanings { get; init; } = new();
    public List<string> FrenchMeanings { get; init; } = new();
    public List<string> GermanMeanings { get; init; } = new();
    public List<string> SpanishMeanings { get; init; } = new();
    public List<string> HungarianMeanings { get; init; } = new();
    public List<string> RussianMeanings { get; init; } = new();
    public List<string> SlovenianMeanings { get; init; } = new();

    public int SenseIndex { get; set; }
    public List<string> Pos { get; set; } = [];
    public List<string> Misc { get; set; } = [];
    public List<string> Field { get; set; } = [];
    public List<string> Dial { get; set; } = [];
    public List<short>? RestrictedToReadingIndices { get; set; }
    public bool IsActiveInLatestSource { get; set; } = true;
}