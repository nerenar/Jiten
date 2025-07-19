using Jiten.Core.Data.Providers.Jimaku;

namespace Jiten.Api.Dtos.Requests;

public class AddJimakuDeckRequest
{
    public required int JimakuId { get; set; }
    public required List<JimakuFile> Files { get; set; }
}