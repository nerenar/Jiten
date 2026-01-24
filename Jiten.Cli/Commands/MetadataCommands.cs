namespace Jiten.Cli.Commands;

public class MetadataCommands
{
    public async Task DownloadMetadata(CliOptions options)
    {
        if (string.IsNullOrEmpty(options.Api))
        {
            Console.WriteLine("Please specify an API to retrieve metadata from.");
            return;
        }

        if (options.Api == "jimaku")
        {
            var range = options.Extra?.Split("-");
            if (range is not { Length: 2 })
            {
                Console.WriteLine("Please specify a range for Jimaku metadata in the form start-end.");
                return;
            }

            await JimakuDownloader.Download(options.Metadata, int.Parse(range[0]), int.Parse(range[1]));
        }
        else
        {
            if (options.Api == "anilist-manga")
                await MetadataDownloader.DownloadMetadata(options.Metadata, options.Api, true, "Volume");
            else
                await MetadataDownloader.DownloadMetadata(options.Metadata, options.Api);
        }
    }
}
