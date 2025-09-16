namespace Jiten.Api.Dtos.Requests;

public class ReportIssueRequest
{
    public required int DeckId { get; set; }
    public required string IssueType { get; set; }
    public required string Comment { get; set; }
}