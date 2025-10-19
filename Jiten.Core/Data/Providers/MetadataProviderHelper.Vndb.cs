using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Jiten.Core.Data;
using Jiten.Core.Data.Providers;
using Jiten.Core.Data.Providers.Vndb;

namespace Jiten.Core;

public static partial class MetadataProviderHelper
{
    public static async Task<List<Metadata>> VndbSearchApi(string query)
    {
        List<VndbRequestResult> requestResults = new List<VndbRequestResult>();

        VnDbRequestPageResult? result = new VnDbRequestPageResult();
        var filter = new List<object> { "search", "=", query };

        var requestContent = new StringContent(JsonSerializer.Serialize(new
                                                                        {
                                                                            filters = filter, fields =
                                                                                "id,title,released,description,titles{main,official,lang,title,latin},image{url,sexual}, extlinks{label,url, name}, aliases, rating",
                                                                            results = 10, page = 1
                                                                        }));
        var http = new HttpClient();
        requestContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        var response = await http.PostAsync("https://api.vndb.org/kana/vn", requestContent);

        if (response.IsSuccessStatusCode)
        {
            var contentStream = await response.Content.ReadAsStringAsync();
            var serializerOptions =
                new JsonSerializerOptions { Converters = { new VndbDateTimeConverter() }, PropertyNameCaseInsensitive = true };

            result = JsonSerializer.Deserialize<VnDbRequestPageResult>(contentStream, serializerOptions);

            requestResults.AddRange(result!.Results);
        }

        List<Metadata> metadatas = [];
        foreach (var requestResult in requestResults)
        {
            var metadata = new Metadata
                           {
                               OriginalTitle = requestResult.Titles.FirstOrDefault(t => t.Lang == "ja")?.Title ?? requestResult.Title,
                               RomajiTitle = requestResult.Titles.FirstOrDefault(t => t.Lang == "ja")?.Latin,
                               EnglishTitle = requestResult.Titles.FirstOrDefault(t => t.Lang == "en")?.Title,
                               ReleaseDate = requestResult.Released,
                               Description = Regex.Replace(requestResult.Description ?? "", @"\[.*\]", ""),
                               Links = [new Link { LinkType = LinkType.Vndb, Url = $"https://vndb.org/{requestResult.Id}" }],
                               Image = requestResult.Image?.Url, Aliases = requestResult.Aliases, Rating = (int)Math.Round(requestResult.Rating ?? 0)
                           };

            metadatas.Add(metadata);
        }

        return metadatas;
    }

    public static async Task<Metadata?> VndbApi(string id)
    {
        VndbRequestResult requestResult = new VndbRequestResult();

        VnDbRequestPageResult? result = new VnDbRequestPageResult();
        var filter = new List<object> { "id", "=", id };

        var requestContent = new StringContent(JsonSerializer.Serialize(new
                                                                        {
                                                                            filters = filter, fields =
                                                                                "id,title,released,description,titles{main,official,lang,title,latin},image{url,sexual}, extlinks{label,url, name}, aliases, rating",
                                                                        }));
        var http = new HttpClient();
        requestContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        var response = await http.PostAsync("https://api.vndb.org/kana/vn", requestContent);

        if (response.IsSuccessStatusCode)
        {
            var contentStream = await response.Content.ReadAsStringAsync();
            var serializerOptions =
                new JsonSerializerOptions { Converters = { new VndbDateTimeConverter() }, PropertyNameCaseInsensitive = true };

            result = JsonSerializer.Deserialize<VnDbRequestPageResult>(contentStream, serializerOptions);

            if (result!.Results.Any())
                requestResult = result!.Results[0];
            else
                return null;
        }

        return new Metadata
               {
                   OriginalTitle = requestResult.Titles.FirstOrDefault(t => t.Lang == "ja")?.Title ?? requestResult.Title,
                   RomajiTitle = requestResult.Titles.FirstOrDefault(t => t.Lang == "ja")?.Latin,
                   EnglishTitle = requestResult.Titles.FirstOrDefault(t => t.Lang == "en")?.Title, ReleaseDate = requestResult.Released,
                   Description = Regex.Replace(requestResult.Description ?? "", @"\[.*\]", ""),
                   Links = [new Link { LinkType = LinkType.Vndb, Url = $"https://vndb.org/{requestResult.Id}" }],
                   Image = requestResult.Image?.Url, Aliases = requestResult.Aliases, Rating = (int)Math.Round(requestResult.Rating ?? 0)
               };
    }
}