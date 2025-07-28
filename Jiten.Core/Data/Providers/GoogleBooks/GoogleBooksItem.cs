namespace Jiten.Core.Data.Providers.GoogleBooks;

public class GoogleBooksItem
{
    public required string Id { get; set; }
    public required string SelfLink { get; set; }
    public required GoogleBooksVolumeInfo VolumeInfo { get; set; }
}