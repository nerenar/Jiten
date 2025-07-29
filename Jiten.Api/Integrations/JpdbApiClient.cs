using System.Net;

namespace Jiten.Api.Integrations;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public class JpdbApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _rateLimitSemaphore;
    private DateTime _lastRequestTime = DateTime.MinValue;
    private readonly TimeSpan _rateLimitDelay = TimeSpan.FromSeconds(0.5);

    public JpdbApiClient(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
            throw new ArgumentNullException(nameof(apiKey));

        ServicePointManager.ServerCertificateValidationCallback = 
            (sender, certificate, chain, sslPolicyErrors) => true;

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        _rateLimitSemaphore = new SemaphoreSlim(1, 1);
    }

    /// <summary>
    /// Gets vocabulary IDs with specific states from all user decks
    /// </summary>
    /// <param name="blacklistedAsKnown">Consider blacklisted words as known</param>
    /// <param name="dueAsKnown">Consider due words as known</param>
    /// <param name="suspendedAsKnown">Consider suspended words as known</param>
    /// <returns>List of vocabulary IDs that match the target states</returns>
    public async Task<List<long>> GetFilteredVocabularyIds(bool blacklistedAsKnown = false, bool dueAsKnown = false,
                                                           bool suspendedAsKnown = false)
    {
        try
        {
            // Step 1: Get list of decks
            var deckIds = await GetUserDecks();

            // Step 2: Get vocabulary from all decks
            var allVocabulary = new List<VocabularyIdPair>();
            foreach (var deckId in deckIds)
            {
                var deckVocabulary = await GetDeckVocabulary(deckId);
                allVocabulary.AddRange(deckVocabulary);
            }

            allVocabulary = allVocabulary
                            .GroupBy(v => v.Id1)
                            .Select(g => g.First())
                            .ToList();

            // Step 3: Lookup vocabulary info and filter by states
            var filteredIds = await LookupAndFilterVocabulary(allVocabulary, blacklistedAsKnown, dueAsKnown, suspendedAsKnown);

            return filteredIds;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error getting filtered vocabulary IDs: {ex.Message}", ex);
        }
    }

    private async Task<List<long>> GetUserDecks()
    {
        var requestBody = new { fields = new[] { "id" } };

        var response = await MakeApiRequestAsync("https://jpdb.io/api/v1/list-user-decks", requestBody);

        using var document = JsonDocument.Parse(response);
        var decks = document.RootElement.GetProperty("decks");

        var deckIds = new List<long>();
        foreach (var deck in decks.EnumerateArray())
        {
            if (deck.GetArrayLength() > 0)
                deckIds.Add(deck[0].GetInt64());
        }

        return deckIds;
    }

    private async Task<List<VocabularyIdPair>> GetDeckVocabulary(long deckId)
    {
        var requestBody = new { id = deckId, fetch_occurences = false };

        var response = await MakeApiRequestAsync("https://jpdb.io/api/v1/deck/list-vocabulary", requestBody);

        using var document = JsonDocument.Parse(response);
        var vocabulary = document.RootElement.GetProperty("vocabulary");

        var vocabularyPairs = new List<VocabularyIdPair>();
        foreach (var vocabItem in vocabulary.EnumerateArray())
        {
            var vocabArray = vocabItem.EnumerateArray().ToArray();
            if (vocabArray.Length >= 2)
            {
                vocabularyPairs.Add(new VocabularyIdPair { Id1 = vocabArray[0].GetInt64(), Id2 = vocabArray[1].GetInt64() });
            }
        }

        return vocabularyPairs;
    }

    private async Task<List<long>> LookupAndFilterVocabulary(List<VocabularyIdPair> vocabularyPairs, bool blacklistedAsKnown = false,
                                                             bool dueAsKnown = false, bool suspendedAsKnown = false)
    {
        const int chunkSize = 2500;
        var filteredIds = new List<long>();
        var targetStates = new HashSet<string> { "never-forget", "known" };

        // Add optional states based on parameters
        if (blacklistedAsKnown) targetStates.Add("blacklisted");
        if (dueAsKnown) targetStates.Add("due");
        if (suspendedAsKnown) targetStates.Add("suspended");

        // Process vocabulary in chunks
        for (int i = 0; i < vocabularyPairs.Count; i += chunkSize)
        {
            var chunk = vocabularyPairs.Skip(i).Take(chunkSize).ToList();
            var lookupList = chunk.Select(vp => new[] { vp.Id1, vp.Id2 }).ToArray();
            var requestBody = new { list = lookupList, fields = new[] { "vid", "card_level", "card_state" } };

            var response = await MakeApiRequestAsync("https://jpdb.io/api/v1/lookup-vocabulary", requestBody);

            using var document = JsonDocument.Parse(response);
            var vocabularyInfo = document.RootElement.GetProperty("vocabulary_info");

            foreach (var vocabInfo in vocabularyInfo.EnumerateArray())
            {
                JsonElement[] infoArray = vocabInfo.EnumerateArray().ToArray();
                if (infoArray.Length < 3) continue;
                var id = infoArray[0].GetInt64();
                JsonElement statesElement = infoArray[2];

                if (statesElement.ValueKind != JsonValueKind.Array) continue;

                // Optimize state checking with array enumeration
                bool matchesTargetState = false;
                foreach (var stateElement in statesElement.EnumerateArray())
                {
                    var state = stateElement.GetString();
                    if (state == null || !targetStates.Contains(state)) continue;
                    
                    matchesTargetState = true;
                    break;
                }

                if (matchesTargetState)
                {
                    filteredIds.Add(id);
                }
            }
        }

        return filteredIds;
    }

    private async Task<string> MakeApiRequestAsync(string url, object requestBody)
    {
        await _rateLimitSemaphore.WaitAsync();

        try
        {
            // Enforce rate limit
            var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
            if (timeSinceLastRequest < _rateLimitDelay)
            {
                var delayTime = _rateLimitDelay - timeSinceLastRequest;
                await Task.Delay(delayTime);
            }

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            _lastRequestTime = DateTime.UtcNow;

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"API request failed with status {response.StatusCode}: {errorContent}");
            }

            return await response.Content.ReadAsStringAsync();
        }
        finally
        {
            _rateLimitSemaphore.Release();
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _rateLimitSemaphore?.Dispose();
    }
}

public class VocabularyIdPair
{
    public long Id1 { get; set; }
    public long Id2 { get; set; }
}