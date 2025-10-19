using System.Text.Json;
using Jiten.Core.Data;
using Jiten.Core.Data.Providers;
using Jiten.Core.Data.Providers.GoogleBooks;
using Jiten.Core.Data.Providers.Vndb;

namespace Jiten.Core;

public static partial class MetadataProviderHelper
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
                                                  ReleaseDate = i.VolumeInfo.PublishedDate, Description = i.VolumeInfo.Description
                                                      ?.Replace("<wbr>", Environment.NewLine)
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

        if (result == null)
            return null;

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
}