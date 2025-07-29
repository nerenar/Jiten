using Jiten.Core;
using Jiten.Core.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Jiten.Api.Controllers;

[ApiController]
[Route("api/frequency-list")]
public class FrequencyListController(JitenDbContext context) : ControllerBase
{
    [HttpGet("download")]
    [ResponseCache(Duration = 60 * 60 * 24)]
    [EnableRateLimiting("download")]
    public async Task<IResult> GetFrequencyList([FromQuery] MediaType? mediaType = null, string downloadType = "yomitan")
    {
        var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        string path = Path.Join(configuration["StaticFilesPath"], "yomitan");

        string fileName, filePath;
        byte[] bytes;
        switch (downloadType)
        {
            case "yomitan":
                fileName = mediaType == null ? "jiten_freq_global.zip" : $"jiten_freq_{mediaType.ToString()}.zip";
                filePath = Path.Join(path, fileName);

                if (!System.IO.File.Exists(filePath))
                {
                    return Results.NotFound($"Frequency list not found: {fileName}");
                }

                bytes = await System.IO.File.ReadAllBytesAsync(filePath);
                return Results.File(bytes, "application/zip", fileName);

            case "csv":
            default:
                fileName = mediaType == null ? "jiten_freq_global.csv" : $"jiten_freq_{mediaType.ToString()}.csv";
                filePath = Path.Join(path, fileName);

                if (!System.IO.File.Exists(filePath))
                {
                    return Results.NotFound($"Frequency list not found: {fileName}");
                }

                bytes = await System.IO.File.ReadAllBytesAsync(filePath);
                return Results.File(bytes, "text/csv", fileName);
        }
    }
}