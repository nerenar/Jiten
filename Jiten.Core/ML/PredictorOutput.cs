using Microsoft.ML.Data;

namespace Jiten.Cli.ML;

public class PredictorOutput
{
    [ColumnName("label")]
    public long[] PredictedLabel { get; set; }


  
    public int PredictedDifficultyClass => (int)PredictedLabel[0];

}