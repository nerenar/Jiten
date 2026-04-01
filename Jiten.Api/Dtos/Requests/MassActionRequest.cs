namespace Jiten.Api.Dtos.Requests;

public class MassActionRequest
{
    public int[]? StateFilter { get; set; }
    public string? DateType { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }

    public required string Action { get; set; }
    public int? TargetState { get; set; }
    public int? PushDays { get; set; }
    public int? StaggerBatchSize { get; set; }

    public int Offset { get; set; }
    public int Limit { get; set; } = 50;
}
