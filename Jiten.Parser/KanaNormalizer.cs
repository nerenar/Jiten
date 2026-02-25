namespace Jiten.Parser;

public class KanaNormalizer
{
    private static readonly Dictionary<char, char> KanaToVowel = BuildKanaToVowelMap();

    private static Dictionary<char, char> BuildKanaToVowelMap()
    {
        var map = new Dictionary<char, char>();
        foreach (char c in "おこそとのほもよろをごぞどぼぽょオコソトノホモヨロヲゴゾドボポョ") map[c] = 'う';
        foreach (char c in "うくすつぬふむゆるぐずづぶぷゅウクスツヌフムユルグズヅブプュ") map[c] = 'う';
        foreach (char c in "えけせてねへめれげぜでべぺエケセテネヘメレゲゼデベペ") map[c] = 'え';
        foreach (char c in "いきしちにひみりぎじぢびぴイキシチニヒミリギジヂビピ") map[c] = 'い';
        foreach (char c in "あかさたなはまやらわがざだばぱゃアカサタナハマヤラワガザダバパャ") map[c] = 'あ';
        return map;
    }

    public static string Normalize(string input)
    {
        if (string.IsNullOrEmpty(input) || input.IndexOf('ー') == -1)
            return input;

        var sb = new System.Text.StringBuilder(input.Length);

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];

            if (c == 'ー' && i > 0)
            {
                sb.Append(KanaToVowel.GetValueOrDefault(input[i - 1], 'ー'));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}