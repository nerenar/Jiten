using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Jiten.Core.Data;
using Jiten.Core.Data.Providers;
using Jiten.Core.Data.Providers.Anilist;
using Jiten.Core.Data.Providers.GoogleBooks;
using Jiten.Core.Data.Providers.Tmdb;
using Jiten.Core.Data.Providers.Vndb;

namespace Jiten.Core;

public static class MetadataProviderHelper
{
    public static async Task<List<Metadata>> GoogleBooksSearchApi(string query)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://www.googleapis.com/books/v1/volumes?q={query}");
        var http = new HttpClient();
        var response = await http.SendAsync(request);

        List<Metadata> metadatas = [];

        if (!response.IsSuccessStatusCode) return metadatas;

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
                                                  Description = i.VolumeInfo.Description?.Replace("<wbr>", Environment.NewLine)
                                                                 .Replace("<br>", Environment.NewLine),
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

        return metadatas;
    }

    public static async Task<Metadata?> GoogleBooksApi(string id)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://www.googleapis.com/books/v1/volumes/{id}");
        var http = new HttpClient();
        var response = await http.SendAsync(request);

        if (!response.IsSuccessStatusCode) return null;

        var contentStream = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<GoogleBooksItem>(contentStream,
                                                                 new JsonSerializerOptions
                                                                 {
                                                                     Converters = { new VndbDateTimeConverter() },
                                                                     PropertyNameCaseInsensitive = true
                                                                 });

        return new Metadata
               {
                   OriginalTitle = result.VolumeInfo.Title, RomajiTitle = null, EnglishTitle = null,
                   ReleaseDate = result.VolumeInfo.PublishedDate,
                   Description = result.VolumeInfo.Description?.Replace("<wbr>", Environment.NewLine).Replace("<br>", Environment.NewLine),
                   Links =
                   [
                       new Link
                       {
                           LinkType = LinkType.GoogleBooks,
                           Url = $"https://www.google.co.jp/books/edition/{result.VolumeInfo.Title}/{result.Id}"
                       }
                   ],
                   Image = result.VolumeInfo.ImageLinks?.Thumbnail
               };
    }

    public static async Task<List<Metadata>> AnilistNovelSearchApi(string query)
    {
        return await AnilistSearchApi(query, "NOVEL");
    }

    public static async Task<List<Metadata>> AnilistMangaSearchApi(string query)
    {
        return await AnilistSearchApi(query, "MANGA");
    }

    public static async Task<List<Metadata>> AnilistSearchApi(string query, string format)
    {
        var requestBody = new
                          {
                              query = @"
        query ($search: String, $type: MediaType, $format: MediaFormat) {
          Page {
            media (search: $search, type: $type, format: $format) {
              id
              idMal
              description
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
            return [];

        var contentStream = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<AnilistResult>(contentStream,
                                                               new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return result?.Data?.Page?.Media.Select(media => new Metadata
                                                         {
                                                             OriginalTitle = media.Title.Native, RomajiTitle = media.Title.Romaji,
                                                             EnglishTitle = media.Title.English, ReleaseDate = media.ReleaseDate,
                                                             Description = Regex.Replace(media.Description ?? "", "<.*?>", "").Trim(), Links =
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

    public static async Task<Metadata?> AnilistApi(int id)
    {
        var requestBody = new
                          {
                              query = """
                                              query ($id: Int) {
                                                  Media (id: $id) {
                                                    id
                                                    idMal
                                                    description
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
                                      """,
                              variables = new { id = id }
                          };

        var httpClient = new HttpClient();
        var requestContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync("https://graphql.anilist.co", requestContent);

        if (!response.IsSuccessStatusCode)
            return null;

        var contentStream = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<AnilistResult>(contentStream,
                                                               new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var media = result?.Data?.Media;

        if (media == null)
            return null;

        return new Metadata
               {
                   OriginalTitle = media.Title.Native, RomajiTitle = media.Title.Romaji, EnglishTitle = media.Title.English,
                   ReleaseDate = media.ReleaseDate, Description = Regex.Replace(media.Description ?? "", "<.*?>", "").Trim(), Links =
                   [
                       new Link { LinkType = LinkType.Anilist, Url = $"https://anilist.co/manga/{media.Id}" }
                   ],
                   Image = media.CoverImage.ExtraLarge
               };
    }


    public static async Task<List<Metadata>> VndbSearchApi(string query)
    {
        List<VndbRequestResult> requestResults = new List<VndbRequestResult>();

        VnDbRequestPageResult? result = new VnDbRequestPageResult();
        var filter = new List<object> { "search", "=", query };

        var requestContent = new StringContent(JsonSerializer.Serialize(new
                                                                        {
                                                                            filters = filter, fields =
                                                                                "id,title,released,description,titles{main,official,lang,title,latin},image{url,sexual}, extlinks{label,url, name}",
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
                               ReleaseDate = requestResult.Released, Description = Regex.Replace(requestResult.Description ?? "", @"\[.*\]", ""),
                               Links = [new Link { LinkType = LinkType.Vndb, Url = $"https://vndb.org/{requestResult.Id}" }],
                               Image = requestResult.Image?.Url
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
                                                                                "id,title,released,description,titles{main,official,lang,title,latin},image{url,sexual}, extlinks{label,url, name}"
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
                   Image = requestResult.Image?.Url
               };
    }

    public static async Task<Metadata> TmdbMovieApi(string tmdbId, string tmdbApiKey)
    {
        var http = new HttpClient();

        var response = await http.GetAsync($"https://api.themoviedb.org/3/movie/{tmdbId}?api_key={tmdbApiKey}");
        if (!response.IsSuccessStatusCode) return new Metadata();

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<TmdbMovie>(content);

        if (result == null)
            return new Metadata();

        if (result.PosterPath != null)
            result.PosterPath = $"https://image.tmdb.org/t/p/w500/{result.PosterPath}";

        var links = new List<Link>();
        if (result.ImdbId != null)
        {
            links.Add(new Link { LinkType = LinkType.Imdb, Url = $"https://www.imdb.com/title/{result.ImdbId}" });
        }

        links.Add(new Link { LinkType = LinkType.Tmdb, Url = $"https://www.themoviedb.org/movie/{tmdbId}" });

        return new Metadata
               {
                   OriginalTitle = result.OriginalTitle, EnglishTitle = result.Title, ReleaseDate = result.ReleaseDate, Links = links,
                   Image = result.PosterPath, Description = result.Description
               };
    }

    public static async Task<Metadata> TmdbTvApi(string tmdbId, string tmdbApiKey)
    {
        var http = new HttpClient();

        var response = await http.GetAsync($"https://api.themoviedb.org/3/tv/{tmdbId}?api_key={tmdbApiKey}");
        if (!response.IsSuccessStatusCode) return new Metadata();

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<TmdbTv>(content);

        if (result == null)
            return new Metadata();

        if (result.PosterPath != null)
            result.PosterPath = $"https://image.tmdb.org/t/p/w500/{result.PosterPath}";

        return new Metadata
               {
                   OriginalTitle = result.OriginalName, EnglishTitle = result.Name, ReleaseDate = result.FirstAirDate,
                   Image = result.PosterPath,
                   Links = [new Link { LinkType = LinkType.Tmdb, Url = $"https://www.themoviedb.org/tv/{tmdbId}" }],
                   Description = result.Description
               };
    }
}