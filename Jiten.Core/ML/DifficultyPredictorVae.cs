using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jiten.Cli.ML;
using Jiten.Core;
using Jiten.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

public class VAEPredictionResult
{
    public double DifficultyScore { get; set; }
    public double Difficulty0To100 { get; set; }
    public double RawScore { get; set; }
    public double Uncertainty { get; set; }
    public string MethodUsed { get; set; }
    public long TimeMs { get; set; }
}

public class InferenceParamsFile
{
    public PreprocessingSection preprocessing { get; set; }
    public QuantileSection quantile_transformer { get; set; }
    public MinMaxSection minmax_scaler { get; set; }
    public PCASection pca { get; set; }
    public MetaSection meta { get; set; }
}

public class PreprocessingSection
{
    public double[] imputer_medians { get; set; }
    public double[] scaler_lambdas { get; set; } // PowerTransformer lambdas
    public double[] scaler_means { get; set; } // Means after transform
    public double[] scaler_scales { get; set; } // Scales after transform
    public string scaler_method { get; set; } // "yeo-johnson" or "box-cox"
    public bool scaler_standardize { get; set; } // Whether to standardize
}


public class QuantileSection
{
    public double[] quantiles { get; set; } // sorted ascending
    public int n_quantiles { get; set; }
    public string output_distribution { get; set; }
}

public class MinMaxSection
{
    public double[] data_min { get; set; }
    public double[] data_max { get; set; }
    public double[] feature_range { get; set; } // usually [0,5] or [0,1] depending; we expect original mm.feature_range
}

public class PCASection
{
    public double[][] components { get; set; } // components[0] is first PC
    public double[] mean { get; set; }
    public int n_components { get; set; }
}

public class MetaSection
{
    public string best_method { get; set; }
    public double alpha { get; set; }
    public int n_features { get; set; }
    public int latent_dim { get; set; }
}

public class DifficultyParams
{
    [JsonPropertyName("pca_components")]
    public double[][] PcaComponents { get; set; }

    [JsonPropertyName("pca_mean")]
    public double[] PcaMean { get; set; }

    [JsonPropertyName("minmax_min")]
    public double[] MinmaxMin { get; set; }

    [JsonPropertyName("minmax_max")]
    public double[] MinmaxMax { get; set; }

    [JsonPropertyName("minmax_range")]
    public double[] MinmaxRange { get; set; }

    [JsonPropertyName("best_method")]
    public string BestMethod { get; set; }

    [JsonPropertyName("alpha")]
    public double Alpha { get; set; }

    [JsonPropertyName("n_features")]
    public int NFeatures { get; set; }

    [JsonPropertyName("latent_dim")]
    public int LatentDim { get; set; }
}


public class VAEPredictorONNX
{
    private readonly InferenceSession _encoderSession;
    private readonly InferenceSession _difficultySession;
    private readonly InferenceParamsFile _params;
    private readonly string _encoderInputName;
    private readonly string _difficultyInputName;

    public VAEPredictorONNX(string encoderOnnxPath, string difficultyOnnxPath, string inferenceParamsJsonPath)
    {
        if (!File.Exists(encoderOnnxPath)) throw new FileNotFoundException("Encoder ONNX model not found", encoderOnnxPath);
        if (!File.Exists(difficultyOnnxPath)) throw new FileNotFoundException("Difficulty ONNX model not found", difficultyOnnxPath);
        if (!File.Exists(inferenceParamsJsonPath)) throw new FileNotFoundException("Params JSON not found", inferenceParamsJsonPath);

        _encoderSession = new InferenceSession(encoderOnnxPath);
        _difficultySession = new InferenceSession(difficultyOnnxPath);

        var jsonText = File.ReadAllText(inferenceParamsJsonPath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        _params = JsonSerializer.Deserialize<InferenceParamsFile>(jsonText, options);

        // Try to detect input name
        _encoderInputName = _encoderSession.InputMetadata.Keys.FirstOrDefault() ?? "input";
        _difficultyInputName = _difficultySession.InputMetadata.Keys.FirstOrDefault() ?? "latent_input";

    }

    // Helper: impute and robust-scale a single row
    private float[] Preprocess(double[] rawFeatures)
    {
        int n = _params.meta.n_features;
        if (rawFeatures.Length != n) throw new ArgumentException($"Expected {n} features, got {rawFeatures.Length}");

        var outArr = new float[n];
        for (int i = 0; i < n; i++)
        {
            double v = rawFeatures[i];

            // Impute
            if (double.IsNaN(v) || double.IsInfinity(v))
                v = _params.preprocessing.imputer_medians[i];

            // PowerTransformer: Apply power transformation then standardize
            double transformed = ApplyPowerTransform(v, _params.preprocessing.scaler_lambdas[i], _params.preprocessing.scaler_method);

            // Standardize if enabled (usually True)
            if (_params.preprocessing.scaler_standardize)
            {
                double standardized = (transformed - _params.preprocessing.scaler_means[i]) / _params.preprocessing.scaler_scales[i];
                outArr[i] = (float)standardized;
            }
            else
            {
                outArr[i] = (float)transformed;
            }
        }

        return outArr;
    }
    
    private double RunDifficultyPredictor(double[] latentVector)
    {
        var dims = new int[] { 1, latentVector.Length };
        var tensor = new DenseTensor<float>(dims);

        for (int j = 0; j < latentVector.Length; j++)
            tensor[0, j] = (float)latentVector[j];

        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(_difficultyInputName, tensor) };

        using (var results = _difficultySession.Run(inputs))
        {
            var first = results.First();
            var asTensor = first.AsTensor<float>();
            return asTensor[0, 0]; // Single output value
        }
    }

    private double ApplyPowerTransform(double x, double lambda, string method)
    {
        if (method == "yeo-johnson")
        {
            return YeoJohnsonTransform(x, lambda);
        }
        else if (method == "box-cox")
        {
            if (x <= 0) x = 1e-8;
            return BoxCoxTransform(x, lambda);
        }
        else
        {
            throw new ArgumentException($"Unknown power transform method: {method}");
        }
    }

    private double YeoJohnsonTransform(double x, double lambda)
    {
        const double eps = 1e-8;
        if (Math.Abs(lambda) < eps)
        {
            if (x >= 0) return Math.Log(x + 1);
            else return -Math.Log(-x + 1);
        }
        else if (Math.Abs(lambda - 2.0) < eps)
        {
            if (x >= 0) return -Math.Log(x + 1);
            else return Math.Log(-x + 1);
        }
        else
        {
            if (x >= 0) return (Math.Pow(x + 1, lambda) - 1) / lambda;
            else return -(Math.Pow(-x + 1, 2 - lambda) - 1) / (2 - lambda);
        }
    }

    private double BoxCoxTransform(double x, double lambda)
    {
        const double eps = 1e-8;
        if (x <= 0) throw new ArgumentException("Box-Cox transform requires positive values");
        if (Math.Abs(lambda) < eps) return Math.Log(x);
        else return (Math.Pow(x, lambda) - 1) / lambda;
    }


    private double[] RunEncoder(float[] preprocessedFeatures)
    {
        // Create DenseTensor of shape [1, n_features]
        var dims = new int[] { 1, preprocessedFeatures.Length };
        var tensor = new DenseTensor<float>(dims);

        for (int j = 0; j < preprocessedFeatures.Length; j++)
            tensor[0, j] = preprocessedFeatures[j];

        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(_encoderInputName, tensor) };

        using (var results = _encoderSession.Run(inputs))
        {
            // Assume the first output is z_mean vector shape [1, latent_dim]
            var first = results.First();
            var asTensor = first.AsTensor<float>();
            var arr = asTensor.ToArray();
            return Array.ConvertAll(arr, x => (double)x);
        }
    }

    // PCA project to first component: replicate sklearn PCA.transform(...)[0]
    private double ProjectPCA_FirstComponent(double[] zMean)
    {
        // center
        int d = _params.pca.mean.Length;
        if (zMean.Length != d) throw new ArgumentException("zMean length mismatch PCA mean");

        double[] centered = new double[d];
        for (int i = 0; i < d; i++) centered[i] = zMean[i] - _params.pca.mean[i];

        // dot with first component
        double[] pc0 = _params.pca.components[0]; // length d
        double proj = 0.0;
        for (int i = 0; i < d; i++) proj += centered[i] * pc0[i];
        return proj;
    }

    private double MinMaxSingle(double raw)
    {
        double min = _params.minmax_scaler.data_min[0];
        double max = _params.minmax_scaler.data_max[0];

        if (max - min == 0.0) return 0.0;
        double scaled = (raw - min) / (max - min);
        scaled = Math.Max(0.0, Math.Min(1.0, scaled)); // Clamp

        double fr0 = 0.0, fr1 = 1.0;
        if (_params.minmax_scaler.feature_range != null && _params.minmax_scaler.feature_range.Length == 2)
        {
            fr0 = _params.minmax_scaler.feature_range[0];
            fr1 = _params.minmax_scaler.feature_range[1];
        }

        return fr0 + scaled * (fr1 - fr0);
    }

    // Quantile transform for single value using quantiles array interpolation to [0,1]
    // This reproduces sklearn QuantileTransformer(output_distribution='uniform').transform on 1D
    private double QuantileTransformSingle(double raw)
    {
        var q = _params.quantile_transformer.quantiles;
        int n = q.Length;
        if (n == 0) return 0.0;

        if (raw <= q[0]) return 0.0;
        if (raw >= q[n - 1]) return 1.0;

        int lo = 0, hi = n - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (raw < q[mid]) hi = mid;
            else lo = mid;
        }

        double ql = q[lo];
        double qh = q[hi];
        if (qh - ql == 0.0) return (double)lo / (n - 1);

        double frac = (raw - ql) / (qh - ql);
        double mappedLo = (double)lo / (n - 1);
        double mappedHi = (double)hi / (n - 1);
        return mappedLo + frac * (mappedHi - mappedLo);
    }

    public VAEPredictionResult Predict(double[] rawFeatures)
    {
        var sw = Stopwatch.StartNew();

        // 1) Preprocess features
        var preprocessed = Preprocess(rawFeatures);

        // 2) Run encoder to get latent representation
        double[] zMean = RunEncoder(preprocessed);

        // 3) Run difficulty predictor to get raw difficulty
        double difficulty_raw = RunDifficultyPredictor(zMean);

        // 4) Apply scaling
        double difficulty_quantile = QuantileTransformSingle(difficulty_raw) * 5.0;
        double difficulty_minmax = MinMaxSingle(difficulty_raw);

        double alpha = _params.meta.alpha;
        double difficulty_hybrid = alpha * difficulty_quantile + (1.0 - alpha) * difficulty_minmax;

        sw.Stop();

        return new VAEPredictionResult
               {
                   DifficultyScore = difficulty_hybrid,
                   Difficulty0To100 = difficulty_hybrid * 20.0,
                   RawScore = difficulty_raw,
                   MethodUsed = "semi_supervised_predictor",
                   TimeMs = sw.ElapsedMilliseconds
               };
    }
}

public class DifficultyPredictorVae(DbContextOptions<JitenDbContext> dbOptions, string scriptPath)
{
    private readonly List<string> _featureOrder = new()
                                                  {
                                                      "Ttr", "AverageSentenceLength", "DialoguePercentage", "KanjiRatio", "KangoPercentage",
                                                      "WagoPercentage", "GairaigoPercentage", "VerbPercentage", "ParticlePercentage",
                                                      "AvgWordPerSentence", "ReadabilityScore", "LogicalConnectorRatio", "ModalMarkerRatio",
                                                      "RelativeClauseMarkerRatio", "MetaphorMarkerRatio", "AvgLogFreqRank",
                                                      "StdLogFreqRank", "AvgLogObsFreq", "StdLogObsFreq", "LowFreqRankPerc",
                                                      "AvgReadingFreqRank", "MedianReadingFreqRank", "AvgReadingObsFreq",
                                                      "MedianReadingObsFreq", "AvgReadingFreqPerc", "AvgCustomScorePerWord",
                                                      "StdCustomWordScore", "PercCustomScoreAboveSoftcapStart", "ratio_negative_conj",
                                                      "ratio_polite_conj", "ratio_conditional_conj", "ratio_passive_causative_conj",
                                                      "ratio_potential_conj", "ratio_volitional_conj", "ratio_imperative_conj",
                                                      "ratio_te_form_conj", "ratio_past_conj", "ratio_stem_conj", "ratio_garu_conj",
                                                      "ratio_seemingness_conj", "ratio_shimau_conj", "ratio_contracted_conj",
                                                      "ratio_other_conj", "RatioConjugations", "ratio_noun_pos", "ratio_verb_pos",
                                                      "ratio_adj_pos", "ratio_adv_pos", "ratio_conjunc_pos", "ratio_aux_pos",
                                                      "ratio_inter_pos", "ratio_fix_pos", "ratio_pn_pos", "ratio_exp_pos", "ratio_other_pos"
                                                  };

    // Same feature order as in your training script

    public async Task<float> PredictDifficulty(Deck deck, MediaType mediaType)
    {
        try
        {
            // 1. Create a temporary MLInputData (some fields might be dummies for live prediction)
            var mlInputData = new MLInputData
                              {
                                  DifficultyScore = 0, // Not used for prediction, can be dummy
                                  TextFilePath = deck.OriginalTitle, // Or some identifier
                                  MediaType = (int)mediaType, OriginalFileName = deck.OriginalTitle
                              };

            // 2. Extract features using your existing FeatureExtractor logic
            ExtractedFeatures extractedFeatures;
            using (var context = new JitenDbContext(dbOptions)) // Create a new context instance
            {
                if (deck == null) throw new InvalidOperationException("Parser returned null deck.");

                // Populate a dummy MLInputData since ProcessFileAsync expects it
                // Manually construct ExtractedFeatures from the deck, then call other extraction methods.
                // This is like inlining parts of ProcessFileAsync.
                extractedFeatures = new ExtractedFeatures
                                    {
                                        Filename = mlInputData.OriginalFileName, DifficultyRating = mlInputData.DifficultyScore
                                    };

                if (deck.CharacterCount == 0 || deck.WordCount == 0)
                    throw new Exception($"Received empty deck metrics from parser for: {deck.OriginalTitle}");

                deck.MediaType = (MediaType)mlInputData.MediaType;

                extractedFeatures.CharacterCount = deck.CharacterCount;
                extractedFeatures.WordCount = deck.WordCount;
                extractedFeatures.UniqueWordCount = deck.UniqueWordCount;
                extractedFeatures.UniqueWordOnceCount = deck.UniqueWordUsedOnceCount;
                extractedFeatures.UniqueKanjiCount = deck.UniqueKanjiCount;
                extractedFeatures.UniqueKanjiOnceCount = deck.UniqueKanjiUsedOnceCount;
                extractedFeatures.SentenceCount = deck.SentenceCount;
                extractedFeatures.AverageSentenceLength = deck.AverageSentenceLength;
                extractedFeatures.DialoguePercentage = deck.DialoguePercentage;
                if (extractedFeatures.WordCount > 0)
                    extractedFeatures.Ttr = extractedFeatures.UniqueWordCount / extractedFeatures.WordCount;
                else extractedFeatures.Ttr = 0;

                var deckWords = deck.DeckWords.ToList();

                MLHelper.ExtractCharacterCounts(deck.RawText.RawText, extractedFeatures);
                await MLHelper.ExtractFrequencyStats(context, deckWords, extractedFeatures);
                MLHelper.ExtractConjugationStats(deckWords, extractedFeatures);
                MLHelper.ExtractReadabilityScore(deckWords, extractedFeatures);
                MLHelper.ExtractSemanticComplexity(deckWords, extractedFeatures);
            }

            // 3. Convert ExtractedFeatures to dictionary for JSON serialization
            var featureMap = GetFeatureMap(extractedFeatures);
            var featuresDict = new Dictionary<string, object>();
            var features = new double[_featureOrder.Count];
            ;
            for (var i = 0; i < _featureOrder.Count; i++)
            {
                string featureName = _featureOrder[i];
                if (featureMap.TryGetValue(featureName, out double value))
                {
                    features[i] = value;
                    if (double.IsNaN(value) || double.IsInfinity(value))
                    {
                        featuresDict[featureName] = null; // JSON will handle this
                    }
                    else
                    {
                        featuresDict[featureName] = value;
                    }
                }
                else
                {
                    Console.WriteLine($"Warning: Feature '{featureName}' not found in extracted features map.");
                    featuresDict[featureName] = null;
                }
            }

            var predictor = new VAEPredictorONNX(
                                                 "Y:\\CODE\\Jiten\\Shared\\resources\\vae_model_semisupervised\\vae_encoder.onnx",
                                                 "Y:\\CODE\\Jiten\\Shared\\resources\\vae_model_semisupervised\\difficulty_predictor.onnx",
                                                 "Y:\\CODE\\Jiten\\Shared\\resources\\vae_model_semisupervised\\inference_params.json"
                                                );


            var timer = Stopwatch.StartNew();
            var result = predictor.Predict(features);

            Console.WriteLine($"Difficulty Score (0-5): {result.DifficultyScore:F3}");
            Console.WriteLine($"Difficulty Score (0-100): {result.Difficulty0To100:F3}");
            Console.WriteLine($"Raw Predictor Score: {result.RawScore:F3}");
            Console.WriteLine($"Method Used: {result.MethodUsed}");
            Console.WriteLine($"Time: {timer.ElapsedMilliseconds}ms");

            return (float)result.DifficultyScore;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in VAE prediction: {ex.Message}");
            throw;
        }
    }

    private Dictionary<string, double> GetFeatureMap(ExtractedFeatures features)
    {
        var map = new Dictionary<string, double>
                  {
                      { "CharacterCount", features.CharacterCount }, { "WordCount", features.WordCount },
                      { "UniqueWordCount", features.UniqueWordCount }, { "UniqueWordOnceCount", features.UniqueWordOnceCount },
                      { "UniqueKanjiCount", features.UniqueKanjiCount }, { "UniqueKanjiOnceCount", features.UniqueKanjiOnceCount },
                      { "SentenceCount", features.SentenceCount }, { "Ttr", features.Ttr },
                      { "AverageSentenceLength", features.AverageSentenceLength }, { "DialoguePercentage", features.DialoguePercentage },
                      { "TotalCount", features.TotalCount }, { "KanjiCount", features.KanjiCount },
                      { "HiraganaCount", features.HiraganaCount }, { "KatakanaCount", features.KatakanaCount },
                      { "OtherCount", features.OtherCount }, { "KanjiRatio", features.KanjiRatio },
                      { "HiraganaRatio", features.HiraganaRatio }, { "KatakanaRatio", features.KatakanaRatio },
                      { "OtherRatio", features.OtherRatio }, { "KangoPercentage", features.KangoPercentage },
                      { "WagoPercentage", features.WagoPercentage }, { "GairaigoPercentage", features.GairaigoPercentage },
                      { "VerbPercentage", features.VerbPercentage }, { "ParticlePercentage", features.ParticlePercentage },
                      { "AvgWordPerSentence", features.AvgWordPerSentence }, { "ReadabilityScore", features.ReadabilityScore },
                      { "LogicalConnectorRatio", features.LogicalConnectorRatio }, { "ModalMarkerRatio", features.ModalMarkerRatio },
                      { "RelativeClauseMarkerRatio", features.RelativeClauseMarkerRatio },
                      { "MetaphorMarkerRatio", features.MetaphorMarkerRatio }, { "AvgLogFreqRank", features.AvgLogFreqRank },
                      { "StdLogFreqRank", features.StdLogFreqRank }, { "MinFreqRank", features.MinFreqRank },
                      { "AvgLogObsFreq", features.AvgLogObsFreq }, { "StdLogObsFreq", features.StdLogObsFreq },
                      { "MaxObsFreq", features.MaxObsFreq },
                      { "LowFreqRankPerc", features.LowFreqRankPerc }, /* { "LowFreqObsPerc", features.LowFreqObsPerc },*/
                      { "AvgReadingFreqRank", features.AvgReadingFreqRank }, { "MedianReadingFreqRank", features.MedianReadingFreqRank },
                      { "AvgReadingObsFreq", features.AvgReadingObsFreq }, { "MedianReadingObsFreq", features.MedianReadingObsFreq },
                      { "AvgReadingFreqPerc", features.AvgReadingFreqPerc }, { "AvgCustomScorePerWord", features.AvgCustomScorePerWord },
                      { "StdCustomWordScore", features.StdCustomWordScore },
                      { "PercCustomScoreAboveSoftcapStart", features.PercCustomScoreAboveSoftcapStart },
                      { "TotalConjugations", features.TotalConjugations }
                  };

        foreach (var kvp in features.ConjugationCategoryCounts)
        {
            map[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in features.ConjugationCategoryRatios)
        {
            map[kvp.Key] = kvp.Value;
        }

        map["RatioConjugations"] = features.RatioConjugations;

        foreach (var kvp in features.PosCategoryCounts)
        {
            map[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in features.PosCategoryRatios)
        {
            map[kvp.Key] = kvp.Value;
        }

        return map;
    }
}