using System.Text;
using WanaKanaShaapu;

namespace Jiten.Core.Utils;

public static class TextNormalizationHelper
{
    /// <summary>
    /// Normalises input text for Japanese parsing by converting:
    /// 1. Uppercase ASCII letters to fullwidth
    /// 2. Lowercase romaji to hiragana
    /// 3. Halfwidth digits to fullwidth
    /// 4. Remaining halfwidth lowercase letters to fullwidth
    /// </summary>
    public static string NormaliseForParsing(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Convert uppercase to fullwidth first so WanaKana won't convert them
        var result = text.ToFullWidthUppercaseLetters();

        // Extract katakana positions and replace with placeholders
        // WanaKana.ToHiragana converts katakana to hiragana, which we don't want
        var katakanaPositions = new List<(int Index, char Char)>();
        var sb = new StringBuilder(result.Length);

        for (int i = 0; i < result.Length; i++)
        {
            char c = result[i];
            if (IsKatakana(c))
            {
                katakanaPositions.Add((sb.Length, c));
                sb.Append('\uFFFD');
            }
            else
            {
                sb.Append(c);
            }
        }

        result = WanaKana.ToHiragana(sb.ToString());

        // Restore katakana characters
        sb = new StringBuilder(result);
        foreach (var (index, katakanaChar) in katakanaPositions)
        {
            if (index < sb.Length)
                sb[index] = katakanaChar;
        }

        result = sb.ToString();
        result = result.ToFullWidthDigits();
        result = result.ToFullWidthLowercaseLetters();

        return result;
    }

    public static bool ContainsRomaji(string text)
    {
        foreach (var c in text)
        {
            if (c is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
                return true;
        }
        return false;
    }

    /// <summary>
    /// Normalises non-standard romaji variants to their standard Hepburn equivalents.
    /// Uses lookahead to protect existing standard forms (e.g. "shi", "chi", "tsu").
    /// </summary>
    public static string NormaliseRomaji(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var sb = new StringBuilder(text.Length + 4);
        var lower = text.ToLowerInvariant();

        for (int i = 0; i < lower.Length; i++)
        {
            char c = lower[i];
            char next = i + 1 < lower.Length ? lower[i + 1] : '\0';
            char next2 = i + 2 < lower.Length ? lower[i + 2] : '\0';

            switch (c)
            {
                case 't' when next == 'u' && next2 != 's':
                    // "tu" → "tsu", but protect "tsu" already present
                    sb.Append("tsu");
                    i++;
                    break;
                case 't' when next == 'i' && next2 != 'c':
                    // "ti" → "chi", but protect existing sequences starting with "tic..."
                    sb.Append("chi");
                    i++;
                    break;
                case 's' when next == 'i' && next2 != 'h':
                    // "si" → "shi", but protect "shi" already present
                    sb.Append("shi");
                    i++;
                    break;
                case 'h' when next == 'u' && next2 != 'f':
                    // "hu" → "fu"
                    sb.Append("fu");
                    i++;
                    break;
                case 'z' when next == 'i':
                    sb.Append("ji");
                    i++;
                    break;
                case 'd' when next == 'u':
                    sb.Append("zu");
                    i++;
                    break;
                case 'd' when next == 'i':
                    sb.Append("ji");
                    i++;
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }

    private static bool IsKatakana(char c)
    {
        return (c >= '\u30A0' && c <= '\u30FF') ||  // Katakana block
               (c >= '\u31F0' && c <= '\u31FF') ||  // Katakana phonetic extensions
               (c >= '\uFF65' && c <= '\uFF9F');    // Halfwidth katakana
    }
}
