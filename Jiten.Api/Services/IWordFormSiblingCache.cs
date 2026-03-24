namespace Jiten.Api.Services;

public class WordFormInfo
{
    public byte[] KanjiReadingIndexes { get; set; } = [];
    public byte[] KanaReadingIndexes { get; set; } = [];
}

public interface IWordFormSiblingCache
{
    byte[]? GetKanaIndexesForKanji(int wordId, byte kanjiReadingIndex);
    byte[]? GetKanjiIndexesForKana(int wordId, byte kanaReadingIndex);
    WordFormInfo? GetWordFormInfo(int wordId);
    void Reload();
}
