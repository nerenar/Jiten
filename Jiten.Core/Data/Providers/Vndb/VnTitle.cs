namespace Jiten.Core.Data.Providers.Vndb;

public class VnTitle
{
    public required string Title { get; set; }
    public required string Latin { get; set; }
    public required string Lang { get; set; }
    public bool Official { get; set; }
    public bool Main { get; set; }
}