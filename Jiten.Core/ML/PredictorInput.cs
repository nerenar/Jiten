using Microsoft.ML.Data;

namespace Jiten.Cli.ML;

public class PredictorInput
{
    [ColumnName("float_input")]
    [VectorType(65)]
    public float[] Features { get; set; }
}