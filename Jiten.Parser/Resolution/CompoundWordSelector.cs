using Jiten.Core.Data;
using Jiten.Core.Data.JMDict;
using Jiten.Parser.Data.Redis;

namespace Jiten.Parser.Resolution;

internal static class CompoundWordSelector
{
    public static async Task<int?> FindValidCompoundWordId(
        List<int> wordIds,
        IJmDictCache jmDictCache,
        bool expressionOnly = false,
        bool isKana = true)
    {
        try
        {
            var wordCache = await jmDictCache.GetWordsAsync(wordIds);

            // First pass: prefer Expression POS (e.g. そうする "to do so" over 奏する "to play music")
            // When multiple expressions match (e.g. 異にする vs 事にする), pick the best by priority
            int? bestExprWordId = null;
            int bestExprScore = int.MinValue;
            foreach (var wordId in wordIds)
            {
                if (!wordCache.TryGetValue(wordId, out var word)) continue;

                var posList = word.CachedPOS;
                if (posList.Contains(PartOfSpeech.Expression))
                {
                    int score = word.GetPriorityScore(isKana);
                    if (score > bestExprScore || (score == bestExprScore && (bestExprWordId == null || wordId < bestExprWordId.Value)))
                    {
                        bestExprScore = score;
                        bestExprWordId = wordId;
                    }
                }
            }

            if (bestExprWordId.HasValue)
                return bestExprWordId;

            if (expressionOnly) return null;

            // Second pass: collect all valid compound candidates and pick best by priority
            int? bestWordId = null;
            int bestScore = int.MinValue;
            foreach (var wordId in wordIds)
            {
                if (!wordCache.TryGetValue(wordId, out var word)) continue;

                var posList = word.CachedPOS;
                if (posList.Any(PosMapper.IsValidCompoundPOS))
                {
                    int score = word.GetPriorityScore(isKana);
                    if (score > bestScore || (score == bestScore && (bestWordId == null || wordId < bestWordId.Value)))
                    {
                        bestScore = score;
                        bestWordId = wordId;
                    }
                }
            }

            return bestWordId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Warning] Error validating compound POS: {ex.Message}");
        }

        return null;
    }
}
