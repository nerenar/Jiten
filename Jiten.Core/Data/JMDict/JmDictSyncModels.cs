namespace Jiten.Core.Data.JMDict;

public class SyncEntry
{
    public int WordId { get; set; }
    public List<SyncForm> KanjiForms { get; set; } = [];
    public List<SyncForm> KanaForms { get; set; } = [];
    public List<SyncSense> Senses { get; set; } = [];
}

public class SyncForm
{
    public string Text { get; set; } = "";
    public JmDictFormType FormType { get; set; }
    public List<string> Priorities { get; set; } = [];
    public List<string> InfoTags { get; set; } = [];
    public bool IsNoKanji { get; set; }
    public List<string> Restrictions { get; set; } = [];
}

public class SyncSense
{
    public int SenseIndex { get; set; }
    public List<string> Pos { get; set; } = [];
    public List<string> Misc { get; set; } = [];
    public List<string> Field { get; set; } = [];
    public List<string> Dial { get; set; } = [];
    public List<string> StagK { get; set; } = [];
    public List<string> StagR { get; set; } = [];
    public List<string> EnglishMeanings { get; set; } = [];
    public List<string> DutchMeanings { get; set; } = [];
    public List<string> FrenchMeanings { get; set; } = [];
    public List<string> GermanMeanings { get; set; } = [];
    public List<string> SpanishMeanings { get; set; } = [];
    public List<string> HungarianMeanings { get; set; } = [];
    public List<string> RussianMeanings { get; set; } = [];
    public List<string> SlovenianMeanings { get; set; } = [];
}
