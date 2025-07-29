namespace Jiten.Api.Dtos.Requests;

public class JpdbImportRequest
{
    public string ApiKey { get; set; } = "";
    public bool BlacklistedAsKnown { get; set; }
    public bool DueAsKnown { get; set; }
    public bool SuspendedAsKnown { get; set; }
}