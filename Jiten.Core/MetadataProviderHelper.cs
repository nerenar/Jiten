using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Jiten.Core.Data;
using Jiten.Core.Data.Providers;
using Jiten.Core.Data.Providers.Anilist;
using Jiten.Core.Data.Providers.GoogleBooks;
using Jiten.Core.Data.Providers.Vndb;

namespace Jiten.Core;

public class MetadataProviderHelper
{
    public static async Task<List<Metadata>> GoogleBooksApi(string query)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://www.googleapis.com/books/v1/volumes?q={query}");
        var http = new HttpClient();
        var response = await http.SendAsync(request);

        List<Metadata> metadatas = [];

        if (response.IsSuccessStatusCode)
        {
            var contentStream = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<GoogleBooksRequestResult>(contentStream,
                                                                              new JsonSerializerOptions
                                                                              {
                                                                                  Converters = { new VndbDateTimeConverter() },
                                                                                  PropertyNameCaseInsensitive = true
                                                                              });

            metadatas = result!.Items.Select(i => new Metadata
                                                  {
                                                      OriginalTitle = i.VolumeInfo.Title, RomajiTitle = null, EnglishTitle = null,
                                                      ReleaseDate = i.VolumeInfo.PublishedDate,
                                                      Links =
                                                      [
                                                          new Link
                                                          {
                                                              LinkType = LinkType.GoogleBooks,
                                                              Url = $"https://www.google.co.jp/books/edition/{i.VolumeInfo.Title}/{i.Id}"
                                                          }
                                                      ],
                                                      Image = i.VolumeInfo.ImageLinks?.Thumbnail
                                                  }).ToList();
        }

        return metadatas;
    }

    public static async Task<List<Metadata>> AnilistNovelApi(string query)
    {
        return await AnilistApi(query, "NOVEL");
    }

    public static async Task<List<Metadata>> AnilistMangaApi(string query)
    {
        return await AnilistApi(query, "MANGA");
    }

    public static async Task<List<Metadata>> AnilistApi(string query, string format)
    {
        var requestBody = new
                          {
                              query = @"
        query ($search: String, $type: MediaType, $format: MediaFormat) {
          Page {
            media (search: $search, type: $type, format: $format) {
              id
              idMal
              title {
                romaji
                english
                native
              }
              startDate {
                day
                month
                year
              }
              bannerImage
              coverImage {
                extraLarge
              }
            }
          }
        }",
                              variables = new { search = query, type = "MANGA", format = format }
                          };

        var httpClient = new HttpClient();
        var requestContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync("https://graphql.anilist.co", requestContent);

        if (!response.IsSuccessStatusCode)
        {
            return new List<Metadata>();
        }

        var contentStream = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<AnilistResult>(contentStream,
                                                               new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return result?.Data?.Page?.Media.Select(media => new Metadata
                                                         {
                                                             OriginalTitle = media.Title.Native, RomajiTitle = media.Title.Romaji,
                                                             EnglishTitle = media.Title.English, ReleaseDate = media.ReleaseDate, Links =
                                                             [
                                                                 new Link
                                                                 {
                                                                     LinkType = LinkType.Anilist,
                                                                     Url = $"https://anilist.co/manga/{media.Id}"
                                                                 }
                                                             ],
                                                             Image = media.CoverImage.ExtraLarge
                                                         }).ToList() ?? [];
    }


    public static async Task<List<Metadata>> VndbApi(string query)
    {
        List<VndbRequestResult> requestResults = new List<VndbRequestResult>();

        VnDbRequestPageResult? result = new VnDbRequestPageResult();
        var filter = new List<object> { "search", "=", query };

        var requestContent = new StringContent(JsonSerializer.Serialize(new
                                                                        {
                                                                            filters = filter, fields =
                                                                                "id,title,released,titles{main,official,lang,title,latin},image{url,sexual}, extlinks{label,url, name}",
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
                               Links = [new Link { LinkType = LinkType.Vndb, Url = $"https://vndb.org/{requestResult.Id}" }],
                               Image = requestResult.Image?.Url
                           };

            metadatas.Add(metadata);
        }

        return metadatas;
    }
}
