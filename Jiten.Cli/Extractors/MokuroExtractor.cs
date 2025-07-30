using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.IdentityModel.Tokens;

namespace Jiten.Cli;

public class MokuroExtractor
{
    public async Task<string> Extract(string? filePath, bool verbose)
    {
        string?[] files = [];

        // TODO: Handle subfolders separately
        if (Directory.Exists(filePath))
        {
            var jsonFiles = Directory.GetFiles(filePath, "*.json", SearchOption.AllDirectories);
            var mokuroFiles = Directory.GetFiles(filePath, "*.mokuro", SearchOption.AllDirectories);
            files = jsonFiles.Concat(mokuroFiles).ToArray();
        }
        else
        {
            files = [filePath];
        }

        if (verbose)
        {
            Console.WriteLine($"Found {files.Length} files to extract.");

            foreach (var file in files)
            {
                Console.WriteLine(file);
            }
        }

        StringBuilder extractedText = new();

        foreach (var file in files)
        {
            if (file == null) continue;

            if (verbose)
            {
                Console.WriteLine($"Processing file: {file}");
            }

            try
            {
                var json = JsonDocument.Parse(await File.ReadAllTextAsync(file));
                var rootElement = json.RootElement;
                bool isNewMokuro = file.EndsWith(".mokuro", StringComparison.InvariantCultureIgnoreCase);

                if (isNewMokuro)
                {
                    JsonElement pages = rootElement.GetProperty("pages");
                    foreach (var page in pages.EnumerateArray())
                    {
                        if (!page.TryGetProperty("blocks", out JsonElement pageBlocks)) continue;
                        foreach (JsonElement block in pageBlocks.EnumerateArray())
                        {
                            ProcessBlock(block);
                        }
                    }
                }
                else
                {
                    JsonElement blocks = rootElement.GetProperty("blocks");
                    foreach (JsonElement block in blocks.EnumerateArray())
                    {
                        ProcessBlock(block);
                    }
                }
            }
            catch (Exception ex)
            {
                if (verbose)
                {
                    Console.WriteLine($"Error processing file {file}: {ex.Message}");
                }
            }
        }

        return extractedText.ToString();

        void ProcessBlock(JsonElement block)
        {
            var lines = block.GetProperty("lines");
            foreach (JsonElement line in lines.EnumerateArray())
            {
                // Remove publisher info etc
                var cleanedLine = Regex.Replace(line.GetString() ?? string.Empty, "^[０-９Ａ-Ｚａ-ｚ－一．第話第章：年月日に]*$", "");
                // Remove excessive punctuation only lnies
                cleanedLine = Regex.Replace(cleanedLine, "^[．！？]*$", "");
                cleanedLine = Regex.Replace(cleanedLine, "．．．．*", "．．．");
                // Remove single character followed by punctuation or not
                cleanedLine = Regex.Replace(cleanedLine, @"^[\u3040-\u309F\u30A0-\u30FF][．！？]*$", "");
                // Remove romaji string longer than 4 characters, very high change they're badly OCR'd
                cleanedLine = Regex.Replace(cleanedLine, @"[Ａ-Ｚａ-ｚ：／．]{3,}", "");

                cleanedLine = cleanedLine.Trim();

                if (string.IsNullOrWhiteSpace(cleanedLine)) continue;
                extractedText.Append(cleanedLine);
            }

            extractedText.AppendLine();
        }
    }
}