namespace Jiten.Cli.ML;

public class MLInputData
{
    public double DifficultyScore { get; set; }
    public required string TextFilePath { get; set; }
    public int MediaType { get; set; }
    public required string OriginalFileName { get; set; }
}