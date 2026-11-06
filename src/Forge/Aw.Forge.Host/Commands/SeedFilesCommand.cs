using System.Text.Json;
using Aw.Forge.Core;
using Aw.Forge.Core.Ports;
using Aw.Forge.Core.Pulls;
using Aw.Kernel;
using Aw.Kernel.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Forge.Host.Commands;

/// <summary>The 'seed files' command: sets the changed files of a pull request, with their texts, from a JSON file (a list of seed files).</summary>
internal static class SeedFilesCommand
{
    internal const string Name = "seed files";

    internal static readonly CommandSpec Spec = new(
        "seed files <link> --from <file.json> [--cap-reached]", ["from"], [], ["cap-reached"], Positionals: 1);

    internal static Task<int> Run(CommandLine line, IServiceProvider services, JsonSerializerOptions json)
    {
        var forge = Binding.Forge(services);
        var seed = services.GetRequiredService<IForgeSeed>();
        var result = forge.Parse(line.Positionals[0]).Bind(pull => Load(line.FullPath("from"), json).Bind(files =>
            seed.SeedFiles(new SeedFilesRequest(pull, files, line.Flags.Contains("cap-reached")))))
            .Map(_ => new Applied(Ok: true));
        return Task.FromResult(CommandOutput.Write(result, json));
    }

    private static Result<IReadOnlyList<SeedFile>> Load(string path, JsonSerializerOptions json)
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ForgeErrors.Invalid.With($"from: cannot read the file ({ex.Message})");
        }

        return AwJson.Deserialize<IReadOnlyList<SeedFile>>(bytes, json);
    }
}
