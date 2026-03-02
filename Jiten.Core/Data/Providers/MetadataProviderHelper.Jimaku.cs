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
    
    public static async Task<List<JimakuEntry>?> JimakuSearchAsync(
        HttpClient httpClient, string apiKey,
        int? anilistId = null, string? tmdbId = null, string? query = null, bool anime = true)
    {
        var queryParams = new List<string>();
        queryParams.Add($"anime={anime.ToString().ToLowerInvariant()}");
        if (anilistId.HasValue) queryParams.Add($"anilist_id={anilistId.Value}");
        if (tmdbId != null) queryParams.Add($"tmdb_id={tmdbId}");
        if (query != null) queryParams.Add($"query={Uri.EscapeDataString(query)}");

        var url = $"https://jimaku.cc/api/entries/search?{string.Join("&", queryParams)}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Authorization", apiKey);
        var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        var content = await response.Content.ReadAsStringAsync();
        return System.Text.Json.JsonSerializer.Deserialize<List<JimakuEntry>>(content);
    }

    public static async Task JimakuDownloadFileAsync(HttpClient httpClient, string url, string filePath)
    {
        var response = await httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        await using var fs = new FileStream(filePath, FileMode.Create);
        await response.Content.CopyToAsync(fs);
    }
}