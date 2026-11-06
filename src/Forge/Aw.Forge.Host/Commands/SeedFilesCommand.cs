using System.Text.Json;

namespace Aw.Forge.Host.Commands;

/// <summary>The 'seed files' command: sets the changed files of a pull request, with their texts, from a JSON file (a list of seed files).</summary>
internal static class SeedFilesCommand
{
    internal const string Name = "seed files";

    internal static readonly CommandSpec Spec = new(
        "seed files <link> --from <file.json> [--cap-reached]", ["from"], [], ["cap-reached"], Positionals: 1);

    internal static Task<int> Run(CommandLine line, IServiceProvider services, JsonSerializerOptions json) => throw new NotSupportedException();
}
