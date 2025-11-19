namespace Jiten.Parser;

public class KanaNormalizer
{
    // Includes Dakuten (が), Handakuten (ぱ), and Small Kana (ゃ, ょ)
    private const string RowA = "あかさたなはまやらわがざだばぱゃアカサタナハマヤラワガザダバパャ";
    private const string RowI = "いきしちにひみりぎじぢびぴイキシチニヒミリギジヂビピ";
    private const string RowU = "うくすつぬふむゆるぐずづぶぷゅウクスツヌフムユルグズヅブプュ";
    private const string RowE = "えけせてねへめれげぜでべぺエケセテネヘメレゲゼデベペ";
    private const string RowO = "おこそとのほもよろをごぞどぼぽょオコソトノホモヨロヲゴゾドボポョ";

    public static string Normalize(string input)
    {
        if (string.IsNullOrEmpty(input) || input.IndexOf('ー') == -1)
            return input;

        // Use StringBuilder to avoid intermediate allocations
        var sb = new System.Text.StringBuilder(input.Length);

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];

            if (c == 'ー' && i > 0)
            {
                char prev = input[i - 1];

                // JmDict/Standard Rules:
                // O-row + ー -> う (e.g., どー -> どう, NOT どお)
                // U-row + ー -> う
                // E-row + ー -> え (e.g., すげー -> すげえ)
                // I-row + ー -> い
                // A-row + ー -> あ

                if (RowO.Contains(prev)) sb.Append('う');
                else if (RowU.Contains(prev)) sb.Append('う');
                else if (RowE.Contains(prev)) sb.Append('え');
                else if (RowI.Contains(prev)) sb.Append('い');
                else if (RowA.Contains(prev)) sb.Append('あ');
                else sb.Append('ー'); // Keep it if we can't determine vowel (e.g. after Kanji or symbol)
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}