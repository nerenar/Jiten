using System.Text.Json.Serialization;

namespace Jiten.Core.Data.Providers.Jimaku;

public class JimakuFile
{
    [JsonPropertyName("url")] public string Url { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("size")] public int Size { get; set; }
    [JsonPropertyName("last_modified")] public string LastModified { get; set; }
}