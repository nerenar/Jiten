namespace Jiten.Core.Data.Providers.GoogleBooks;

public class GoogleBooksVolumeInfo
{
    public required string Title { get; set; }
    public required string Subtitle { get; set; }
    public required List<string> Authors { get; set; }
    public required string Publisher { get; set; }
    public DateTime PublishedDate { get; set; }
    public string? Description { get; set; }
    public required List<GoogleBooksIndustryIdentifier> IndustryIdentifiers { get; set; }
    public int PageCount { get; set; }
    public required List<string> Categories { get; set; }
    public double AverageRating { get; set; }
    public int RatingsCount { get; set; }
    public required string MaturityRating { get; set; }
    public required string Language { get; set; }
    public required string PreviewLink { get; set; }
    public required string InfoLink { get; set; }
    public required string CanonicalVolumeLink { get; set; }
    public required GoogleBooksImageLinks ImageLinks { get; set; }
}