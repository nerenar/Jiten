namespace Jiten.Core.Data.FSRS;

public class FsrsExportDto
{
    public required DateTime ExportDate { get; set; }
    public required string UserId { get; set; }
    public required int TotalCards { get; set; }
    public required int TotalReviews { get; set; }
    public List<FsrsCardExportDto> Cards { get; set; } = [];
}
