namespace Jiten.Core.Data.Providers.Vndb;

public class VnDbRequestPageResult
{
    public List<VndbRequestResult> Results { get; set; } = new();
    public bool More { get; set; }
}