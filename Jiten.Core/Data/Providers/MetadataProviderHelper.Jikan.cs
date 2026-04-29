using System.Text.Json;
using Jiten.Core.Data;
using Jiten.Core.Data.Providers;
using Jiten.Core.Data.Providers.Jikan;

namespace Jiten.Core;

public static partial class MetadataProviderHelper
{
    private static readonly JsonSerializerOptions JikanJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<List<Metadata>> JikanAnimeSearchApi(string query)
    {
        var httpClient = new HttpClient();
        var encoded = Uri.EscapeDataString(query);
        var response = await httpClient.GetAsync($"https://api.jikan.moe/v4/anime?q={encoded}&order_by=score&limit=10");

        if (!response.IsSuccessStatusCode)
            return [];

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JikanSearchResponse>(content, JikanJsonOptions);

        return result?.Data.Select(media => MapJikanMedia(media, "anime")).ToList() ?? [];
    }

    public static async Task<List<Metadata>> JikanNovelSearchApi(string query)
    {
        var httpClient = new HttpClient();
        var encoded = Uri.EscapeDataString(query);
        var response = await httpClient.GetAsync($"https://api.jikan.moe/v4/manga?q={encoded}&type=lightnovel&order_by=score&limit=10");

        if (!response.IsSuccessStatusCode)
            return [];

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JikanSearchResponse>(content, JikanJsonOptions);

        return result?.Data.Select(media => MapJikanMedia(media, "manga")).ToList() ?? [];
    }

    public static async Task<List<Metadata>> JikanMangaSearchApi(string query)
    {
        var httpClient = new HttpClient();
        var encoded = Uri.EscapeDataString(query);
        var response = await httpClient.GetAsync($"https://api.jikan.moe/v4/manga?q={encoded}&type=manga&order_by=score&limit=10");

        if (!response.IsSuccessStatusCode)
            return [];

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JikanSearchResponse>(content, JikanJsonOptions);

        return result?.Data.Select(media => MapJikanMedia(media, "manga")).ToList() ?? [];
    }

    public static async Task<Metadata?> JikanAnimeApi(int malId)
    {
        var httpClient = new HttpClient();
        var response = await httpClient.GetAsync($"https://api.jikan.moe/v4/anime/{malId}/full");

        if (!response.IsSuccessStatusCode)
            return null;

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JikanResponse<JikanMedia>>(content, JikanJsonOptions);

        if (result?.Data == null)
            return null;

        var metadata = MapJikanMedia(result.Data, "anime");
        metadata.DictionaryEntries = await FetchJikanCharacters(httpClient, "anime", malId);
        return metadata;
    }

    public static async Task<Metadata?> JikanMangaApi(int malId)
    {
        var httpClient = new HttpClient();
        var response = await httpClient.GetAsync($"https://api.jikan.moe/v4/manga/{malId}/full");

        if (!response.IsSuccessStatusCode)
            return null;

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JikanResponse<JikanMedia>>(content, JikanJsonOptions);

        if (result?.Data == null)
            return null;

        var metadata = MapJikanMedia(result.Data, "manga");
        metadata.DictionaryEntries = await FetchJikanCharacters(httpClient, "manga", malId);
        return metadata;
    }

    private static Metadata MapJikanMedia(JikanMedia media, string mediaCategory)
    {
        var aliases = new List<string>(media.TitleSynonyms);
        foreach (var t in media.Titles)
        {
            if (t.Type is "Synonym" or "German" or "French" or "Spanish"
                && !string.IsNullOrWhiteSpace(t.Title)
                && !aliases.Contains(t.Title, StringComparer.OrdinalIgnoreCase))
            {
                aliases.Add(t.Title);
            }
        }

        var genres = media.Genres.Select(g => g.Name)
            .Concat(media.Demographics.Select(d => d.Name))
            .Distinct()
            .ToList();

        var tags = media.Themes
            .Select(t => new MetadataTag { Name = t.Name, Percentage = 80 })
            .ToList();

        var releaseDate = mediaCategory == "anime" ? media.Aired?.From : media.Published?.From;

        return new Metadata
        {
            OriginalTitle = media.TitleJapanese ?? media.Title,
            RomajiTitle = media.Title,
            EnglishTitle = media.TitleEnglish,
            Description = media.Synopsis?.Trim(),
            Image = media.Images?.Jpg?.LargeImageUrl,
            ReleaseDate = releaseDate,
            Links = [new Link { LinkType = LinkType.Mal, Url = $"https://myanimelist.net/{mediaCategory}/{media.MalId}" }],
            Aliases = aliases,
            Rating = media.Score.HasValue ? (int)(media.Score.Value * 10) : null,
            Genres = genres,
            Tags = tags,
            IsAdultOnly = media.Rating?.Contains("Rx") == true,
            IsNotOriginallyJapanese = false,
            Relations = MapJikanRelations(media.Relations)
        };
    }

    private static List<MetadataRelation> MapJikanRelations(List<JikanRelationGroup> relations)
    {
        var result = new List<MetadataRelation>();

        foreach (var group in relations)
        {
            var mapping = MapJikanRelationType(group.Relation);
            if (mapping == null)
                continue;

            foreach (var entry in group.Entry)
            {
                var targetMediaType = MapJikanTypeToMediaType(entry.Type);

                result.Add(new MetadataRelation
                {
                    ExternalId = entry.MalId.ToString(),
                    LinkType = LinkType.Mal,
                    RelationshipType = mapping.Value.Type,
                    TargetMediaType = targetMediaType,
                    SwapDirection = mapping.Value.SwapDirection
                });
            }
        }

        return result;
    }

    private static (DeckRelationshipType Type, bool SwapDirection)? MapJikanRelationType(string relationType)
    {
        return relationType switch
        {
            "Sequel" => (DeckRelationshipType.Sequel, true),
            "Prequel" => (DeckRelationshipType.Sequel, false),
            "Side story" => (DeckRelationshipType.SideStory, true),
            "Spin-off" => (DeckRelationshipType.Spinoff, true),
            "Alternative version" => (DeckRelationshipType.Alternative, false),
            "Alternative setting" => (DeckRelationshipType.Alternative, false),
            "Adaptation" => (DeckRelationshipType.Adaptation, false),
            _ => null
        };
    }

    private static MediaType? MapJikanTypeToMediaType(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "anime" or "tv" or "movie" or "ova" or "ona" or "special" => MediaType.Anime,
            "manga" or "manhwa" or "manhua" or "one-shot" or "doujinshi" => MediaType.Manga,
            "light novel" or "novel" => MediaType.Novel,
            _ => null
        };
    }

    private static async Task<List<DeckDictionaryEntry>> FetchJikanCharacters(
        HttpClient httpClient, string mediaCategory, int malId)
    {
        try
        {
            await Task.Delay(400);
            var listResponse = await httpClient.GetAsync(
                $"https://api.jikan.moe/v4/{mediaCategory}/{malId}/characters");

            if (!listResponse.IsSuccessStatusCode)
                return [];

            var listJson = await listResponse.Content.ReadAsStringAsync();
            var characterList = JsonSerializer.Deserialize<JikanCharactersResponse>(listJson, JikanJsonOptions);

            if (characterList?.Data == null || characterList.Data.Count == 0)
                return [];

            var mainCharacterIds = characterList.Data
                .Where(c => c.Role == "Main")
                .Select(c => c.Character.MalId)
                .Take(25)
                .ToList();

            var names = new List<(string? native, string? firstHint, string? lastHint)>();

            foreach (var charId in mainCharacterIds)
            {
                await Task.Delay(400);
                var charResponse = await httpClient.GetAsync($"https://api.jikan.moe/v4/characters/{charId}");

                if (!charResponse.IsSuccessStatusCode)
                    continue;

                var charJson = await charResponse.Content.ReadAsStringAsync();
                var charResult = JsonSerializer.Deserialize<JikanResponse<JikanCharacterDetail>>(charJson, JikanJsonOptions);

                if (charResult?.Data == null || string.IsNullOrWhiteSpace(charResult.Data.NameKanji))
                    continue;

                string? firstHint = null;
                string? lastHint = null;
                var englishName = charResult.Data.Name;
                if (englishName.Contains(','))
                {
                    var parts = englishName.Split(',', 2);
                    lastHint = parts[0].Trim();
                    firstHint = parts[1].Trim();
                }

                names.Add((charResult.Data.NameKanji, firstHint, lastHint));
            }

            return names.Count > 0 ? BuildDictionaryEntriesFromNames(names) : [];
        }
        catch
        {
            return [];
        }
    }
}
