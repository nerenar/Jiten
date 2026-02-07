namespace Jiten.Core.Data.JMDict;

public class JmDictWordFormFrequency
{
    public int WordId { get; set; }
    public short ReadingIndex { get; set; }
    public int FrequencyRank { get; set; }
    public double FrequencyPercentage { get; set; }
    public double ObservedFrequency { get; set; }
    public int UsedInMediaAmount { get; set; }
}
