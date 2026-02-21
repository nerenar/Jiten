using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Configuration;

namespace Jiten.Parser.Runtime;

internal sealed class ParserRuntimeSettings(
    IConfiguration configuration,
    string dictionaryPath,
    string sudachiConfigPath,
    string sudachiNoUserDicConfigPath)
{
    private static readonly Lazy<ParserRuntimeSettings> CurrentLazy =
        new(Create, LazyThreadSafetyMode.ExecutionAndPublication);

    public IConfiguration Configuration { get; } = configuration;
    public string DictionaryPath { get; } = dictionaryPath;
    public string SudachiConfigPath { get; } = sudachiConfigPath;
    public string SudachiNoUserDicConfigPath { get; } = sudachiNoUserDicConfigPath;

    public static ParserRuntimeSettings Current => CurrentLazy.Value;

    private static ParserRuntimeSettings Create()
    {
        var configuration = new ConfigurationBuilder()
                            .SetBasePath(Directory.GetCurrentDirectory())
                            .AddJsonFile(Path.Combine(Environment.CurrentDirectory, "..", "Shared", "sharedsettings.json"),
                                         optional: true,
                                         reloadOnChange: true)
                            .AddJsonFile(Path.Combine("Shared", "sharedsettings.json"), optional: true, reloadOnChange: true)
                            .AddJsonFile("sharedsettings.json", optional: true, reloadOnChange: true)
                            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                            .AddEnvironmentVariables()
                            .Build();

        var resourcesBasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources");
        return new ParserRuntimeSettings(
            configuration,
            configuration.GetValue<string>("DictionaryPath")!,
            Path.Combine(resourcesBasePath, "sudachi.json"),
            Path.Combine(resourcesBasePath, "sudachi_nouserdic.json"));
    }
}
