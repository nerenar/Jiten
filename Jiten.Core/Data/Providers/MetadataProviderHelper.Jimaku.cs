using Jiten.Core.Data.Providers.Jimaku;

namespace Jiten.Core;

public partial class MetadataProviderHelper
{
    public static async Task<JimakuEntry?> JimakuGetEntryAsync(HttpClient httpClient, string apiKey, int id)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://jimaku.cc/api/entries/{id}");
        request.Headers.Add("Authorization", apiKey);
        var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        var content = await response.Content.ReadAsStringAsync();
        return System.Text.Json.JsonSerializer.Deserialize<JimakuEntry>(content);
    }

    public static async Task<List<JimakuFile>?> JimakuGetFilesAsync(HttpClient httpClient, string apiKey, int id)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://jimaku.cc/api/entries/{id}/files");
        request.Headers.Add("Authorization", apiKey);
        var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        var content = await response.Content.ReadAsStringAsync();
        return System.Text.Json.JsonSerializer.Deserialize<List<JimakuFile>>(content);
    }
    
    public static async Task JimakuDownloadFileAsync(HttpClient httpClient, string url, string filePath)
    {
        var response = await httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        await using var fs = new FileStream(filePath, FileMode.Create);
        await response.Content.CopyToAsync(fs);
    }
}