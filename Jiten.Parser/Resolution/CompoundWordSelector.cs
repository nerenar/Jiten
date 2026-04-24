using Jiten.Core.Data;
using Jiten.Parser.Data;

namespace Jiten.Parser.Resolution;

internal static class CompoundWordSelector
{
    public static int? FindValidCompoundWordId(
        List<int> wordIds,
        Dictionary<int, JmDictWordMeta> wordMeta,
        bool expressionOnly = false,
        bool isKana = true)
    {
        int? bestExprWordId = null;
        int bestExprScore = int.MinValue;
        foreach (var wordId in wordIds)
        {
            if (!wordMeta.TryGetValue(wordId, out var meta)) continue;

            if (Array.IndexOf(meta.Pos, PartOfSpeech.Expression) >= 0)
            {
                int score = meta.GetPriorityScore(isKana);
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

        int? bestWordId = null;
        int bestScore = int.MinValue;
        foreach (var wordId in wordIds)
        {
            if (!wordMeta.TryGetValue(wordId, out var meta)) continue;

            bool hasValidPos = false;
            foreach (var p in meta.Pos)
            {
                if (PosMapper.IsValidCompoundPOS(p))
                {
                    hasValidPos = true;
                    break;
                }
            }

            if (hasValidPos)
            {
                int score = meta.GetPriorityScore(isKana);
                if (score > bestScore || (score == bestScore && (bestWordId == null || wordId < bestWordId.Value)))
                {
                    bestScore = score;
                    bestWordId = wordId;
                }
            }
        }

        return bestWordId;
    }
}
