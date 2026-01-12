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
        result = WanaKana.ToHiragana(result);
        result = result.ToFullWidthDigits();
        result = result.ToFullWidthLowercaseLetters();

        return result;
    }
}
