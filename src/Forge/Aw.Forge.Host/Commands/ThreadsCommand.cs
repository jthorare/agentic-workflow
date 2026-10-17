using System.Text.Json;
using Aw.Kernel;

namespace Aw.Forge.Host.Commands;

/// <summary>The 'threads' command: lists the conversation on a pull request.</summary>
internal static class ThreadsCommand
{
    internal const string Name = "threads";

    internal static readonly CommandSpec Spec = new("threads <link>", [], [], [], Positionals: 1);

    internal static async Task<int> Run(CommandLine line, IServiceProvider services, JsonSerializerOptions json)
    {
        var forge = Binding.Forge(services);
        var result = await forge.Parse(line.Positionals[0]).BindAsync(pull => forge.ListThreads(pull, CancellationToken.None)).ConfigureAwait(false);
        return CommandOutput.Write(result, json);
    }
}
