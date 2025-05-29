namespace Jiten.Core.Data.Providers;

public class Metadata
{
    public string FilePath { get; set; }
    public string OriginalTitle { get; set; } = "Unknown";
    public string? RomajiTitle { get; set; }
    public string? EnglishTitle { get; set; }
    public string? Image { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public List<Link> Links { get; set; } = new();
    public List<Metadata> Children { get; set; } = new();
}