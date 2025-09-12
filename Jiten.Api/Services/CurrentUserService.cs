using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Jiten.Core;
using Jiten.Core.Data;
using Jiten.Core.Data.JMDict;
using Jiten.Core.Data.User;
using Microsoft.EntityFrameworkCore;

namespace Jiten.Api.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor, JitenDbContext jitenDbContext, UserDbContext userContext)
    : ICurrentUserService
{
    public ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public string? UserId
    {
        get
        {
            var user = Principal;
            if (user?.Identity?.IsAuthenticated != true)
                return null;

            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out _))
                return null;

            return userId;
        }
    }

    public async Task<Dictionary<(int WordId, byte ReadingIndex), KnownState>> GetKnownWordsState(
        IEnumerable<(int WordId, byte ReadingIndex)> keys)
    {
        if (!IsAuthenticated)
            return new Dictionary<(int, byte), KnownState>();

        var keysList = keys.ToList();
        if (!keysList.Any())
            return new Dictionary<(int, byte), KnownState>();

        var wordIds = keysList.Select(k => k.WordId).Distinct().ToList();

        var candidates = await userContext.UserKnownWords
                                          .Where(u => u.UserId == UserId && wordIds.Contains(u.WordId))
                                          .ToListAsync();

        return candidates
               .Where(w => keysList.Contains((w.WordId, w.ReadingIndex)))
               .ToDictionary(w => (w.WordId, w.ReadingIndex), w => w.KnownState);
    }

    public async Task<KnownState> GetKnownWordState(int wordId, byte readingIndex)
    {
        if (!IsAuthenticated)
            return KnownState.Unknown;

        var word = await userContext.UserKnownWords.FirstOrDefaultAsync(u => u.UserId == UserId && u.WordId == wordId &&
                                                                             u.ReadingIndex == readingIndex);
        return word?.KnownState ?? KnownState.Unknown;
    }

    public async Task<int> AddKnownWords(IEnumerable<DeckWord> deckWords)
    {
        if (!IsAuthenticated) return 0;
        var words = deckWords?.ToList() ?? [];
        if (words.Count == 0) return 0;;

        var byWordId = words.GroupBy(w => w.WordId).ToDictionary(g => g.Key, g => g.Select(x => x.ReadingIndex).Distinct().ToList());
        var wordIds = byWordId.Keys.ToList();

        // Load needed JMDict words
        var jmdictWords = await jitenDbContext.JMDictWords
                                              .AsNoTracking()
                                              .Where(w => wordIds.Contains(w.WordId))
                                              .Select(w => new { w.WordId, w.ReadingTypes })
                                              .ToListAsync();

        // Determine all (WordId, ReadingIndex) pairs to add, including KanaReading when needed
        var pairs = new HashSet<(int WordId, byte ReadingIndex)>();
        foreach (var jw in jmdictWords)
        {
            if (!byWordId.TryGetValue(jw.WordId, out var indices)) continue;
            foreach (var idx in indices)
            {
                if (idx >= jw.ReadingTypes.Count) continue;
                pairs.Add((jw.WordId, idx));
                if (jw.ReadingTypes[idx] != JmDictReadingType.Reading) continue;

                var kanaIndex = jw.ReadingTypes.FindIndex(t => t == JmDictReadingType.KanaReading);
                if (kanaIndex >= 0)
                    pairs.Add((jw.WordId, (byte)kanaIndex));
            }
        }

        if (pairs.Count == 0) return 0;

        DateTime now = DateTime.UtcNow;
        List<int> pairWordIds = pairs.Select(p => p.WordId).Distinct().ToList();
        List<UserKnownWord> existing = await userContext.UserKnownWords
                                                        .Where(uk => uk.UserId == UserId && pairWordIds.Contains(uk.WordId))
                                                        .ToListAsync();
        var existingSet = existing.ToDictionary(e => (e.WordId, e.ReadingIndex));

        List<UserKnownWord> toInsert = new();
        foreach (var p in pairs)
        {
            if (!existingSet.TryGetValue(p, out var existingUk))
            {
                toInsert.Add(new UserKnownWord
                             {
                                 UserId = UserId!, WordId = p.WordId, ReadingIndex = p.ReadingIndex, LearnedDate = now,
                                 KnownState = KnownState.Known
                             });
            }
            else if (existingUk.KnownState != KnownState.Known)
            {
                existingUk.KnownState = KnownState.Known;
                existingUk.LearnedDate = now;
            }
        }

        if (toInsert.Count > 0)
            await userContext.UserKnownWords.AddRangeAsync(toInsert);

        var updated = existing.Any(e => e.KnownState == KnownState.Known && e.LearnedDate == now) ||
                      existing.Any(e => e.KnownState != KnownState.Known && e.LearnedDate == now);
        if (toInsert.Count > 0 || updated)
        {
            await userContext.SaveChangesAsync();
        }
        
        return (toInsert.Count);
    }

    public async Task AddKnownWord(int wordId, byte readingIndex)
    {
        await AddKnownWords([new DeckWord { WordId = wordId, ReadingIndex = readingIndex }]);
    }

    public async Task RemoveKnownWord(int wordId, byte readingIndex)
    {
        if (!IsAuthenticated) return;

        var jw = await jitenDbContext.JMDictWords.AsNoTracking().Where(w => w.WordId == wordId)
                                     .Select(w => new { w.WordId, w.ReadingTypes }).FirstOrDefaultAsync();
        if (jw == null) return;
        if (readingIndex >= jw.ReadingTypes.Count) return;

        var pairs = new HashSet<(int WordId, byte ReadingIndex)> { (wordId, readingIndex) };
        if (jw.ReadingTypes[readingIndex] == JmDictReadingType.Reading)
        {
            var kanaIndex = jw.ReadingTypes.FindIndex(t => t == JmDictReadingType.KanaReading);
            if (kanaIndex >= 0)
                pairs.Add((wordId, (byte)kanaIndex));
        }

        List<int> pairWordIds = pairs.Select(p => p.WordId).Distinct().ToList();
        List<UserKnownWord> toRemove = await userContext.UserKnownWords
                                                        .Where(uk => uk.UserId == UserId && pairWordIds.Contains(uk.WordId))
                                                        .ToListAsync();
        List<UserKnownWord> removals = toRemove.Where(uk => pairs.Contains((uk.WordId, uk.ReadingIndex))).ToList();
        if (removals.Count == 0) return;
        userContext.UserKnownWords.RemoveRange(removals);
        await userContext.SaveChangesAsync();
    }

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
}