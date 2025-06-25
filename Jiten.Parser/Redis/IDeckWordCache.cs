using Jiten.Core.Data;

namespace Jiten.Parser.Data.Redis;

public interface IDeckWordCache
{
    Task<DeckWord?> GetAsync(DeckWordCacheKey key);
    Task SetAsync(DeckWordCacheKey key, DeckWord word);
}

public record DeckWordCacheKey(string Text, PartOfSpeech PartOfSpeech, string DictionaryForm);