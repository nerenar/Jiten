using Jiten.Api.Dtos;
using Jiten.Core.Data;

namespace Jiten.Api.Services;

public record DeckWordResolveRequest(
    int DeckId,
    Deck Deck,
    DeckDownloadType DownloadType,
    DeckOrder Order,
    int MinFrequency,
    int MaxFrequency,
    bool ExcludeMatureMasteredBlacklisted,
    bool ExcludeAllTrackedWords,
    float? TargetPercentage,
    int? MinOccurrences = null,
    int? MaxOccurrences = null);

public class ResolvedWord
{
    public int WordId { get; set; }
    public byte ReadingIndex { get; set; }
    public int Occurrences { get; set; }
    public int SortOrder { get; set; }
}

public record GlobalDynamicResult(List<ResolvedWord> Words, bool WasTruncated);

public interface IDeckWordResolver
{
    Task<(List<DeckWord>? Words, IResult? Error)> ResolveDeckWords(DeckWordResolveRequest request);
    Task<HashSet<long>> GetStudyDeckWordKeys(List<int> deckIds);
    Task<HashSet<long>> GetStaticDeckWordKeys(List<int> studyDeckIds);
    Task<GlobalDynamicResult> ResolveGlobalDynamicWords(int? minFreq, int? maxFreq, string? posFilter,
        bool excludeKana, bool excludeMatureMasteredBlacklisted, bool excludeAllTrackedWords);
    Task<List<ResolvedWord>> ResolveStaticDeckWords(int studyDeckId, int order);
    Task<HashSet<long>> GetGlobalDynamicWordKeys(int? minFreq, int? maxFreq, string? posFilter);
    Task<HashSet<long>> GetGlobalDynamicWordKeysForWordIds(int? minFreq, int? maxFreq, string? posFilter, List<int> wordIds);
    Task<(int Count, bool WasTruncated)> CountGlobalDynamicWords(int? minFreq, int? maxFreq, string? posFilter, bool excludeKana,
        bool excludeMatureMasteredBlacklisted = false, bool excludeAllTrackedWords = false);
    Task<(int Count, HashSet<long> WordKeys)> CountDeckWords(DeckWordResolveRequest request, bool excludeKana);
    Task<(int Count, HashSet<long> WordKeys)> CountTargetCoverageWords(int deckId, Deck deck, float targetPercentage, bool excludeKana);
    Task<(int Count, HashSet<long> WordKeys)> CountStaticDeckWords(int studyDeckId, bool excludeKana);
}
