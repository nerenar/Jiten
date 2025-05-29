using Jiten.Core.Data;

namespace Jiten.Api.Dtos.Requests;

public class AddMediaRequest
{
    public MediaType MediaType { get; set; }
    public required string OriginalTitle { get; set; }
    public string? RomajiTitle { get; set; }
    public string? EnglishTitle { get; set; }
    public IFormFile? CoverImage { get; set; }
    public IFormFile? File { get; set; }
    public List<Link> Links { get; set; } = new List<Link>();
    public List<AddMediaRequestSubdeck>? Subdecks { get; set; } = new List<AddMediaRequestSubdeck>();
}

public class AddMediaRequestSubdeck
{
    public required string OriginalTitle { get; set; }
    public required IFormFile File { get; set; }
}
