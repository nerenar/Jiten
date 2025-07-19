using Jiten.Core.Data.Providers.Jimaku;

namespace Jiten.Api.Dtos;

public class JimakuResultDto
{
    public JimakuEntry? Entry { get; set; }
    public List<JimakuFile>? Files { get; set; }
}
