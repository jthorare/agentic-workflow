using System.Text.Json;
using Aw.Kernel;

namespace Aw.Forge.Host.Commands;

/// <summary>The 'pull' command: reads a pull request.</summary>
internal static class PullCommand
{
    internal const string Name = "pull";

    internal static readonly CommandSpec Spec = new("pull <link>", [], [], [], Positionals: 1);

    internal static async Task<int> Run(CommandLine line, IServiceProvider services, JsonSerializerOptions json)
    {
        var forge = Binding.Forge(services);
        var result = await forge.Parse(line.Positionals[0]).BindAsync(pull => forge.GetPull(pull, CancellationToken.None)).ConfigureAwait(false);
        return CommandOutput.Write(result, json);
    }
}
