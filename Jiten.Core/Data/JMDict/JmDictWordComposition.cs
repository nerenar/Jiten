namespace Jiten.Core.Data.JMDict;

public class JmDictWordComposition
{
    public int WordId { get; set; }
    public short ReadingIndex { get; set; }
    public short Position { get; set; }
    public int ComponentWordId { get; set; }
    public short ComponentReadingIndex { get; set; }
    public string ComponentSurface { get; set; } = "";

    public JmDictWord Word { get; set; } = null!;
    public JmDictWord Component { get; set; } = null!;
}
