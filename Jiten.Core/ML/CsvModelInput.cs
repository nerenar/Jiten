// using Microsoft.ML.Data;
//
// namespace Jiten.Cli.ML;
//
// public class CsvModelInput
// {
//     // Ensure property names here match usage in GetFeatureColumns() if reflecting on this class,
//     // OR ensure ColumnName attributes match CSV headers EXACTLY for TextLoader.
//
//     [ColumnName("Filename")] // ID Column
//     public string Filename { get; set; }
//
//     [ColumnName("DifficultyRating")] // Target Column
//     public float DifficultyRating { get; set; } // ML.NET prefers float for labels & features
//
//     // Parser features
//     [ColumnName("CharacterCount")] public float CharacterCount { get; set; }
//     [ColumnName("WordCount")] public float WordCount { get; set; }
//     [ColumnName("UniqueWordCount")] public float UniqueWordCount { get; set; }
//     [ColumnName("UniqueWordOnceCount")] public float UniqueWordOnceCount { get; set; }
//     [ColumnName("UniqueKanjiCount")] public float UniqueKanjiCount { get; set; }
//     [ColumnName("UniqueKanjiOnceCount")] public float UniqueKanjiOnceCount { get; set; }
//     [ColumnName("SentenceCount")] public float SentenceCount { get; set; }
//     [ColumnName("Ttr")] public float Ttr { get; set; }
//     [ColumnName("AverageSentenceLength")] public float AverageSentenceLength { get; set; }
//     [ColumnName("DialoguePercentage")] public float DialoguePercentage { get; set; }
//
//     // Calculated counts
//     [ColumnName("TotalCount")] public float TotalCount { get; set; }
//     [ColumnName("KanjiCount")] public float KanjiCount { get; set; }
//     [ColumnName("HiraganaCount")] public float HiraganaCount { get; set; }
//     [ColumnName("KatakanaCount")] public float KatakanaCount { get; set; }
//     [ColumnName("OtherCount")] public float OtherCount { get; set; }
//     [ColumnName("KanjiRatio")] public float KanjiRatio { get; set; }
//     [ColumnName("HiraganaRatio")] public float HiraganaRatio { get; set; }
//     [ColumnName("KatakanaRatio")] public float KatakanaRatio { get; set; }
//     [ColumnName("KanjiToKanaRatio")] public float KanjiToKanaRatio { get; set; }
//
//     // Frequency Stats
//     [ColumnName("AvgLogFreqRank")] public float AvgLogFreqRank { get; set; }
//     [ColumnName("MedianLogFreqRank")] public float MedianLogFreqRank { get; set; }
//     [ColumnName("StdLogFreqRank")] public float StdLogFreqRank { get; set; }
//     [ColumnName("MinFreqRank")] public float MinFreqRank { get; set; }
//     [ColumnName("MaxFreqRank")] public float MaxFreqRank { get; set; }
//     [ColumnName("AvgLogObsFreq")] public float AvgLogObsFreq { get; set; }
//     [ColumnName("MedianLogObsFreq")] public float MedianLogObsFreq { get; set; }
//     [ColumnName("StdLogObsFreq")] public float StdLogObsFreq { get; set; }
//     [ColumnName("MinObsFreq")] public float MinObsFreq { get; set; }
//     [ColumnName("MaxObsFreq")] public float MaxObsFreq { get; set; }
//     [ColumnName("LowFreqRankPerc")] public float LowFreqRankPerc { get; set; }
//     [ColumnName("LowFreqObsPerc")] public float LowFreqObsPerc { get; set; }
//     [ColumnName("AvgReadingFreqRank")] public float AvgReadingFreqRank { get; set; }
//     [ColumnName("MedianReadingFreqRank")] public float MedianReadingFreqRank { get; set; }
//     [ColumnName("AvgReadingObsFreq")] public float AvgReadingObsFreq { get; set; }
//     [ColumnName("MedianReadingObsFreq")] public float MedianReadingObsFreq { get; set; }
//     [ColumnName("AvgReadingFreqPerc")] public float AvgReadingFreqPerc { get; set; }
//     [ColumnName("MedianReadingFreqPerc")] public float MedianReadingFreqPerc { get; set; }
//     [ColumnName("AvgCustomScorePerWord")] public float AvgCustomScorePerWord { get; set; }
//     [ColumnName("MedianCustomWordScore")] public float MedianCustomWordScore { get; set; }
//     [ColumnName("StdCustomWordScore")] public float StdCustomWordScore { get; set; }
//     [ColumnName("MaxCustomWordScore")] public float MaxCustomWordScore { get; set; }
//     [ColumnName("PercCustomScoreAboveSoftcapStart")] public float PercCustomScoreAboveSoftcapStart { get; set; }
//
//     // Conjugation Stats
//     [ColumnName("TotalConjugations")] public float TotalConjugations { get; set; }
//
//     // ConjugationCategoryCounts - Naming convention "conj_{category}_count"
//     [ColumnName("conj_negative_count")] public float conj_negative_count { get; set; }
//     [ColumnName("conj_polite_count")] public float conj_polite_count { get; set; }
//     [ColumnName("conj_conditional_count")] public float conj_conditional_count { get; set; }
//     [ColumnName("conj_passive_causative_count")] public float conj_passive_causative_count { get; set; }
//     [ColumnName("conj_potential_count")] public float conj_potential_count { get; set; }
//     [ColumnName("conj_volitional_count")] public float conj_volitional_count { get; set; }
//     [ColumnName("conj_imperative_count")] public float conj_imperative_count { get; set; }
//     [ColumnName("conj_te_form_count")] public float conj_te_form_count { get; set; }
//     [ColumnName("conj_past_count")] public float conj_past_count { get; set; }
//     [ColumnName("conj_stem_count")] public float conj_stem_count { get; set; }
//     [ColumnName("conj_other_count")] public float conj_other_count { get; set; }
//
//     // ConjugationCategoryRatios - Naming convention "ratio_{category}_conj"
//     [ColumnName("ratio_negative_conj")] public float ratio_negative_conj { get; set; }
//     [ColumnName("ratio_polite_conj")] public float ratio_polite_conj { get; set; }
//     [ColumnName("ratio_conditional_conj")] public float ratio_conditional_conj { get; set; }
//     [ColumnName("ratio_passive_causative_conj")] public float ratio_passive_causative_conj { get; set; }
//     [ColumnName("ratio_potential_conj")] public float ratio_potential_conj { get; set; }
//     [ColumnName("ratio_volitional_conj")] public float ratio_volitional_conj { get; set; }
//     [ColumnName("ratio_imperative_conj")] public float ratio_imperative_conj { get; set; }
//     [ColumnName("ratio_te_form_conj")] public float ratio_te_form_conj { get; set; }
//     [ColumnName("ratio_past_conj")] public float ratio_past_conj { get; set; }
//     [ColumnName("ratio_stem_conj")] public float ratio_stem_conj { get; set; }
//     [ColumnName("ratio_other_conj")] public float ratio_other_conj { get; set; }
//
//     [ColumnName("RatioConjugations")] public float RatioConjugations { get; set; }
//
//     // Timing columns (loaded but not used as features by default)
//     // These names must match your CSV headers if they exist.
//     [ColumnName("TimeDotnetInteractMs")] public float TimeDotnetInteractMs { get; set; }
//     [ColumnName("TimeCharcountMs")] public float TimeCharcountMs { get; set; }
//     [ColumnName("TimeFreqMs")] public float TimeFreqMs { get; set; }
//     [ColumnName("TimeConjMs")] public float TimeConjMs { get; set; }
//     [ColumnName("TimeTotalProcessMs")] public float TimeTotalProcessMs { get; set; }
// }