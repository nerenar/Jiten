using System.Security.Claims;
using Jiten.Core.Data;
using Jiten.Core.Data.User;

namespace Jiten.Api.Services;

public interface ICurrentUserService
{
    string? UserId { get; }
    bool IsAuthenticated { get; }
    ClaimsPrincipal? Principal { get; }
    Task<Dictionary<(int WordId, byte ReadingIndex), KnownState>> GetKnownWordsState(IEnumerable<(int WordId, byte ReadingIndex)> keys, bool getDue = false);
    Task<KnownState> GetKnownWordState(int wordId, byte readingIndex);
    Task<int> AddKnownWords(IEnumerable<DeckWord> deckWords);
    Task AddKnownWord(int wordId, byte readingIndex);
    Task RemoveKnownWord(int wordId, byte readingIndex);
}
