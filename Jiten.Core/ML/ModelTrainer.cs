// // ModelTrainer.cs
//
// using Microsoft.ML;
// using Microsoft.ML.Data;
// using Microsoft.ML.Trainers.LightGbm;
// using System.Diagnostics;
//
// namespace Jiten.Cli.ML;
//
// public class ModelTrainer
// {
//     private readonly MLContext _mlContext;
//
//     public ModelTrainer()
//     {
//         _mlContext = new MLContext(seed: TrainerConfig.RandomState);
//     }
//
//     public void TrainAndSaveModel()
//     {
//         var stopwatch = Stopwatch.StartNew();
//         Console.WriteLine("Starting LightGBM model training process...");
//
//         // 1. Load Data
//         Console.WriteLine($"Loading features from: {TrainerConfig.FeatureCsvPath}");
//         if (!File.Exists(TrainerConfig.FeatureCsvPath))
//         {
//             Console.WriteLine($"ERROR: Feature file not found at {TrainerConfig.FeatureCsvPath}");
//             return;
//         }
//
//         // Define the schema using TextLoader.Columns to map CSV headers to CsvModelInput properties
//         // This is more robust than relying on LoadColumn indices if CsvModelInput has ColumnName attributes.
//         // Or, if CsvModelInput uses LoadColumn(index), make sure indices are perfect.
//         // For this example, assuming CsvModelInput uses ColumnName attributes that match the CSV headers.
//         var textLoader = _mlContext.Data.CreateTextLoader<CsvModelInput>(
//                                                                          separatorChar: ',',
//                                                                          hasHeader: true,
//                                                                          allowQuoting: true,
//                                                                          trimWhitespace: true,
//                                                                          allowSparse:
//                                                                          false // LightGBM handles sparse data, but dense is fine from CSV
//                                                                         );
//
//         IDataView allData = textLoader.Load(TrainerConfig.FeatureCsvPath);
//
//         // Log Min/Max ELO for scaling info (as in Python script)
//         var eloColumn = allData.GetColumn<float>(TrainerConfig.TargetColumnName).ToList();
//         if (!eloColumn.Any())
//         {
//             Console.WriteLine("ERROR: Target column is empty or not found after loading data.");
//             return;
//         }
//
//         Console.WriteLine("--- Scaling Info ---");
//         Console.WriteLine($"Minimum {TrainerConfig.TargetColumnName} in dataset: {eloColumn.Min()}");
//         Console.WriteLine($"Maximum {TrainerConfig.TargetColumnName} in dataset: {eloColumn.Max()}");
//         Console.WriteLine("Use these values for MinMax scaling in the inference logic if needed.");
//         Console.WriteLine("--------------------");
//
//
//         // 2. Prepare Data & Define Features
//         // Get all column names from the loaded data's schema
//         var allSchemaColumns = allData.Schema.Select(col => col.Name).ToList();
//
//         // Determine feature columns by excluding specified ones
//         string[] featureColumnNames = allSchemaColumns
//                                       .Where(colName =>
//                                                  !TrainerConfig.ColumnsToDropProgrammatically.Contains(colName) &&
//                                                  !colName.StartsWith("Time") && // Drop timing columns
//                                                  !colName.StartsWith("conj_") && // Drop conjugation count columns
//                                                  !TrainerConfig.ColumnsToDropExplicitly.Contains(colName)
//                                             ).ToArray();
//
//         Console.WriteLine($"Using {featureColumnNames.Length} features.");
//         if (featureColumnNames.Length == 0)
//         {
//             Console.WriteLine("ERROR: No feature columns selected. Check drop logic and CSV headers.");
//             return;
//         }
//         // Console.WriteLine("Selected features: " + string.Join(", ", featureColumnNames));
//
//
//         // 3. Split Data
//         // We'll use the Test set for early stopping, mimicking the Python script's eval_set.
//         Console.WriteLine($"Splitting data into training and testing sets (Test size: {TrainerConfig.TestSize * 100}%)");
//         DataOperationsCatalog.TrainTestData dataSplit = _mlContext.Data.TrainTestSplit(allData,
//                                                                                        labelColumnName: TrainerConfig.TargetColumnName,
//                                                                                        testFraction: TrainerConfig.TestSize,
//                                                                                        seed: TrainerConfig.RandomState);
//         IDataView trainData = dataSplit.TrainSet;
//         IDataView testData = dataSplit.TestSet; // This will be used as validation set for early stopping
//
//         Console.WriteLine($"Training set rows: {trainData.GetColumn<float>(TrainerConfig.TargetColumnName).Count()}");
//         Console.WriteLine($"Testing set rows: {testData.GetColumn<float>(TrainerConfig.TargetColumnName).Count()}");
//
//         // 4. Define and Train Model Pipeline
//         Console.WriteLine("Defining LightGBM training pipeline...");
//
//         var pipeline = _mlContext.Transforms.Concatenate(TrainerConfig.LgbmOptions.FeatureColumnName, featureColumnNames)
//                                  .Append(_mlContext.Regression.Trainers.LightGbm(TrainerConfig.LgbmOptions));
//         // Note: For early stopping, the LightGbm trainer itself needs the validation data.
//         // The standard pipeline.Fit(trainData) won't use testData for early stopping.
//         // We need to fit the LightGbm estimator directly.
//
//         Console.WriteLine("Starting model training...");
//
//         // To use early stopping, we fit the trainer component directly with validation data
//         var featureProcessingPipeline = _mlContext.Transforms.Concatenate(TrainerConfig.LgbmOptions.FeatureColumnName, featureColumnNames);
//         var preprocessedTrainData = featureProcessingPipeline.Fit(trainData).Transform(trainData);
//         var preprocessedTestData = featureProcessingPipeline.Fit(trainData).Transform(testData); // Use same fitting for consistency
//
//         var lightGbmTrainer = _mlContext.Regression.Trainers.LightGbm(TrainerConfig.LgbmOptions);
//
//         ITransformer trainedModel = null;
//         var modelParameters = lightGbmTrainer.Fit(preprocessedTrainData, preprocessedTestData); // This overload enables early stopping
//
//         // To make it a full prediction pipeline for saving:
//         trainedModel =
//             featureProcessingPipeline.Append(modelParameters)
//                                      .Fit(allData); // Re-fit the preprocessing on allData for schema consistency if needed.
//
//         Console.WriteLine("Model training finished.");
//         // If early stopping was used, LightGBM trainer internally uses the best iteration.
//         // Accessing model.BestIteration equivalent is not straightforwardly exposed in the resulting ITransformer.
//         // The underlying LightGbmModelParameters might have info, but it's less accessible than sklearn.
//         // The console output from LightGBM (if Silent=false) will show early stopping details.
//
//
//         // 5. Evaluate Model
//         Console.WriteLine("Evaluating model on the test set...");
//         IDataView predictions = trainedModel.Transform(testData); // Test data already preprocessed if we went the direct trainer route
//         // If using pipeline.Fit(trainData), then transform testData with full pipeline
//
//         // If trainedModel was from pipeline.Fit(trainData):
//         // IDataView predictions = trainedModel.Transform(testData);
//
//         // If trainedModel from lightGbmTrainer.Fit(preprocessedTrainData, preprocessedTestData)
//         // and then wrapped with featureProcessingPipeline:
//         // predictions are already on preprocessedTestData.
//         // For clarity, let's re-transform the original testData with the full model if it was built that way
//         if (trainedModel != null && trainedModel.CanTransform(testData))
//         {
//             // Check if schema matches
//             predictions = trainedModel.Transform(testData);
//         }
//         else
//         {
//             // This case means trainedModel was just the LightGBM part, not the full preprocessing.
//             // We'd use the 'preprocessedTestData' with the 'modelParameters' part.
//             predictions = modelParameters.Transform(preprocessedTestData);
//         }
//
//
//         RegressionMetrics metrics = _mlContext.Regression.Evaluate(predictions,
//                                                                    labelColumnName: TrainerConfig.TargetColumnName,
//                                                                    scoreColumnName: "Score"); // Default score column name
//
//         Console.WriteLine($"--- Test Set Evaluation Metrics ---");
//         Console.WriteLine($"Mean Absolute Error (MAE):  {metrics.MeanAbsoluteError:F4}");
//         Console.WriteLine($"Mean Squared Error (MSE):   {metrics.MeanSquaredError:F4}");
//         Console.WriteLine($"Root Mean Squared Error (RMSE): {metrics.RootMeanSquaredError:F4}");
//         Console.WriteLine($"R-squared (R²):           {metrics.RSquared:F4}");
//         Console.WriteLine($"-----------------------------------");
//
//         // 6. Feature Importance Analysis (Gain-based from LightGBM)
//         Console.WriteLine("Calculating feature importances (Gain-based)...");
//         if (modelParameters is LightGbmRegressionModelParameters lgbmModelParameters)
//         {
//             var featureGains = lgbmModelParameters.GetFeatureWeights(); // This is an array of floats (gains)
//
//             // Get the names of the features in the concatenated "Features" vector
//             var preprocessedSchema = preprocessedTrainData.Schema;
//             var featureVectorAnnotations = preprocessedSchema[TrainerConfig.LgbmOptions.FeatureColumnName].Annotations;
//             VBuffer<ReadOnlyMemory<char>> slotNames = default;
//             featureVectorAnnotations.GetValue("SlotNames", ref slotNames);
//
//             if (slotNames.Length == featureGains.Length)
//             {
//                 var importances = new List<KeyValuePair<string, float>>();
//                 for (int i = 0; i < slotNames.Length; i++)
//                 {
//                     importances.Add(new KeyValuePair<string, float>(slotNames.GetItemOrDefault(i).ToString(), featureGains[i]));
//                 }
//
//                 var sortedImportances = importances.OrderByDescending(kvp => kvp.Value).ToList();
//
//                 Console.WriteLine($"\n--- Top {TrainerConfig.NFeaturesToDisplay} Features (Gain) ---");
//                 for (int i = 0; i < Math.Min(TrainerConfig.NFeaturesToDisplay, sortedImportances.Count); i++)
//                 {
//                     Console.WriteLine($"{sortedImportances[i].Key}: {sortedImportances[i].Value:F4}");
//                 }
//
//                 Console.WriteLine($"------------------------------------");
//             }
//             else
//             {
//                 Console.WriteLine($"Could not match feature names to gains for importance. Slot names length: {slotNames.Length}, Feature gains length: {featureGains.Length}");
//                 
//                 // Display feature gains without names
//                 var sortedGains = featureGains.Select((gain, index) => new KeyValuePair<int, float>(index, gain))
//                                              .OrderByDescending(kvp => kvp.Value)
//                                              .ToList();
//                 
//                 Console.WriteLine($"\n--- Top {TrainerConfig.NFeaturesToDisplay} Features (Gain by Index) ---");
//                 for (int i = 0; i < Math.Min(TrainerConfig.NFeaturesToDisplay, sortedGains.Count); i++)
//                 {
//                     Console.WriteLine($"Feature #{sortedGains[i].Key}: {sortedGains[i].Value:F4}");
//                 }
//                 
//                 Console.WriteLine($"------------------------------------");
//             }
//         }
//         else
//         {
//             Console.WriteLine("Could not retrieve LightGBM model parameters for feature importance.");
//         }
//
//
//         // 7. Save Model
//         Console.WriteLine($"Saving trained model to: {TrainerConfig.ModelOutputPath}");
//         _mlContext.Model.Save(trainedModel, allData.Schema, TrainerConfig.ModelOutputPath);
//         Console.WriteLine("Model saved successfully.");
//
//         stopwatch.Stop();
//         Console.WriteLine($"Total script execution time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
//
//         // 8. SHAP Analysis
//         Console.WriteLine("\n--- SHAP Analysis ---");
//         Console.WriteLine("SHAP analysis as in the Python script (using the 'shap' library) is not directly available in ML.NET.");
//         Console.WriteLine("For model interpretability in ML.NET, you can use:");
//         Console.WriteLine(" - Permutation Feature Importance (PFI): `_mlContext.Regression.PermutationFeatureImportance(...)`");
//         Console.WriteLine(" - Inspecting tree structures for tree-based models (more complex).");
//         Console.WriteLine("To get SHAP values similar to Python, you would typically:");
//         Console.WriteLine(" 1. Export the trained model to ONNX format if possible (LightGBM can be tricky).");
//         Console.WriteLine(" 2. Load the ONNX model in Python and use ONNX-compatible SHAP explainers.");
//         Console.WriteLine("OR");
//         Console.WriteLine(" 1. Use a .NET SHAP library if a suitable one exists (e.g., experimental ones like SHAPfor.NET).");
//         Console.WriteLine("--------------------");
//     }
// }