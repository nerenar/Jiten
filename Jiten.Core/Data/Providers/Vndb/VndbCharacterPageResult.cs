namespace Jiten.Core.Data.Providers.Vndb;

public class VndbCharacterPageResult
{
    public List<VndbCharacterResult> Results { get; set; } = new();
    public bool More { get; set; }
}
