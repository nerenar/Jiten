// namespace Jiten.Cli.ML;
//
// using Microsoft.ML.Trainers.LightGbm;
//
// public static class TrainerConfig
// {
//     public static string FeatureCsvPath => MLConfig.OutputCsvPath;
//     public const string ModelOutputPath = "lightgbm_difficulty_model.zip"; // ML.NET typically uses .zip
//
//     public const string TargetColumnName = "DifficultyRating"; // Must match the CSV header and CsvModelInput property
//     public const string IdColumnName = "Filename";             // Must match the CSV header and CsvModelInput property
//
//     public static readonly HashSet<string> ColumnsToDropProgrammatically = new HashSet<string>(new[] {
//         IdColumnName,
//         TargetColumnName
//     });
//
//     public static readonly HashSet<string> ColumnsToDropExplicitly = new HashSet<string>(new[] {
//         "CharacterCount",
//         "WordCount",
//         "UniqueWordCount",
//         "SentenceCount",
//         "TotalCount",
//         "KanjiCount",
//         "HiraganaCount",
//         "KatakanaCount",
//         "OtherCount",
//         "MinFreqRank",
//         "TotalConjugations",
//         "KatakanaRatio",
//         "OtherRatio",  
//         "HiraganaRatio", 
//         "UniqueKanjiCount",
//         "UniqueKanjiOnceCount",
//         "UniqueWordOnceCount" 
//     });
//
//     public const float TestSize = 0.2f;
//     public const int RandomState = 42;
//     public const int NFeaturesToDisplay = 30; // For feature importance output
//
//     // LightGBM Training Settings
//     public static LightGbmRegressionTrainer.Options LgbmOptions => new LightGbmRegressionTrainer.Options
//     {
//         LabelColumnName = TargetColumnName,
//         FeatureColumnName = "Features", // Default output name from Concatenate estimator
//         // LossFunction = new L1RegressionLoss(), // Python: 'regression_l1'
//         // Metric = LightGbmRegressionTrainer.Options.RegressionMetricType.Mae, // For internal eval during training if validation set used
//         NumberOfIterations = 2000,       // Python: 'n_estimators'
//         LearningRate = 0.05f,            // Python: 'learning_rate'
//         NumberOfLeaves = 31,             // Python: 'num_leaves'
//         // MaximumTreeDepth = 0,            // Python: 'max_depth': -1 (0 means no limit in ML.NET)
//         // FeatureFraction = 0.8,           // Python: 'feature_fraction'
//         // Subsample = 0.8,                 // Python: 'bagging_fraction'
//         // SubsampleFrequency = 1,          // Python: 'bagging_freq'
//         Silent = false,                   // Python: 'verbose': -1. Set to false to see some LightGBM logs.
//         Seed = RandomState,              // Python: 'seed'
//         EarlyStoppingRound = EarlyStoppingRounds, // Enable early stopping
//         // BoostingType = GradientBoosterType.Gbdt, // Default, Python: 'boosting_type': 'gbdt'
//         // NumberOfThreads = MLConfig.NumThreads, // ML.NET LightGBM often manages this well. Can be set if needed.
//     }; 
//   
//     public const int EarlyStoppingRounds = 100; // Python: EARLY_STOPPING_ROUNDS
// }