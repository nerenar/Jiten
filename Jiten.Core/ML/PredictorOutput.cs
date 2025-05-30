using Microsoft.ML.Data;

namespace Jiten.Cli.ML;

public class PredictorOutput
{
    [ColumnName("variable")]
    public float[] PredictedDifficulty { get; set; }
}