using System.Diagnostics;
using System.Text;
using Jiten.Parser.Diagnostics;

namespace Jiten.Parser;

public partial class MorphologicalAnalyser
{
    private static List<SudachiToken> ParseSudachiOutputToDiagnosticTokens(string rawOutput)
    {
        var tokens = new List<SudachiToken>();
        var lines = rawOutput.Split('\n');

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line == "EOS") continue;

            var parts = line.Split('\t');
            if (parts.Length < 5) continue;

            var posDetail = parts[1].Split(',');
            tokens.Add(new SudachiToken
                       {
                           Surface = parts[0], PartOfSpeech = posDetail.Length > 0 ? posDetail[0] : "",
                           PosDetail = posDetail.Skip(1).ToArray(), NormalizedForm = parts.Length > 2 ? parts[2] : "",
                           DictionaryForm = parts.Length > 3 ? parts[3] : "", Reading = parts.Length > 4 ? parts[4] : ""
                       });
        }

        return tokens;
    }

    private static List<WordInfo> TrackStage(TokenStage stage, List<WordInfo> input, ParserDiagnostics? diagnostics)
    {
        var inputSnapshot = diagnostics != null ? input.Select(w => w.Text).ToList() : null;
        var sw = diagnostics != null ? Stopwatch.StartNew() : null;

        var result = stage.Apply(input);

        if (diagnostics == null)
            return result;

        sw!.Stop();
        var outputSnapshot = result.Select(w => w.Text).ToList();
        diagnostics.TokenStages.Add(new TokenProcessingStage
        {
            StageName = stage.Name,
            StageGroup = stage.Group.ToString(),
            ElapsedMs = sw.Elapsed.TotalMilliseconds,
            InputTokenCount = inputSnapshot!.Count,
            OutputTokenCount = outputSnapshot.Count,
            Modifications = DetectModifications(inputSnapshot, outputSnapshot)
        });

        return result;
    }

    private static List<TokenModification> DetectModifications(List<string> inputTokens, List<string> outputTokens)
    {
        var modifications = new List<TokenModification>();

        int i = 0, j = 0;
        while (i < inputTokens.Count && j < outputTokens.Count)
        {
            if (inputTokens[i] == outputTokens[j])
            {
                i++;
                j++;
                continue;
            }

            var merged = new StringBuilder();
            var mergeStart = i;
            while (i < inputTokens.Count && !merged.ToString().Equals(outputTokens[j]))
            {
                merged.Append(inputTokens[i]);
                i++;

                if (merged.ToString() == outputTokens[j])
                {
                    modifications.Add(new TokenModification
                                      {
                                          Type = "merge", InputTokens = inputTokens.Skip(mergeStart).Take(i - mergeStart).ToArray(),
                                          OutputToken = outputTokens[j], Reason = $"Merged {i - mergeStart} tokens"
                                      });
                    j++;
                    break;
                }
            }

            if (i == mergeStart)
            {
                i++;
                j++;
            }
        }

        while (i < inputTokens.Count)
        {
            modifications.Add(new TokenModification
                              {
                                  Type = "remove", InputTokens = [inputTokens[i]], OutputToken = null, Reason = "Token removed"
                              });
            i++;
        }

        return modifications;
    }
}
