namespace Jiten.Parser;

public class SentenceInfo
{
    public string Text { get; set; } = string.Empty;
    public List<(WordInfo word, int position, int length)> Words { get; set; } = new();
    
    public SentenceInfo(string text)
    {
        Text = text;
    }
}