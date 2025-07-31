namespace Jiten.Core.Data.Providers.GoogleBooks;

public class GoogleBooksVolumeInfo
{
    public required string Title { get; set; }
    public string? Subtitle { get; set; }
    public List<string>? Authors { get; set; }
    public string? Publisher { get; set; }
    public DateTime PublishedDate { get; set; }
    public string? Description { get; set; }
    public List<GoogleBooksIndustryIdentifier>? IndustryIdentifiers { get; set; }
    public int PageCount { get; set; }
    public List<string>? Categories { get; set; }
    public double AverageRating { get; set; }
    public int RatingsCount { get; set; }
    public required string MaturityRating { get; set; }
    public required string Language { get; set; }
    public required string PreviewLink { get; set; }
    public required string InfoLink { get; set; }
    public required string CanonicalVolumeLink { get; set; }
    public GoogleBooksImageLinks? ImageLinks { get; set; }
}