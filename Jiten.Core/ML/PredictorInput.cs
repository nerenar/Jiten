using Microsoft.ML.Data;

namespace Jiten.Cli.ML;

public class PredictorInput
{
    [ColumnName("float_input")]
    [VectorType(38)]
    public float[] Features { get; set; }
}