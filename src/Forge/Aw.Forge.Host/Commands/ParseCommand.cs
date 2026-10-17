using System.Text.Json;

namespace Aw.Forge.Host.Commands;

/// <summary>The 'parse' command: parses a link against the allowed hosts and sends nothing.</summary>
internal static class ParseCommand
{
    internal const string Name = "parse";

    internal static readonly CommandSpec Spec = new("parse <link>", [], [], [], Positionals: 1);

    internal static Task<int> Run(CommandLine line, IServiceProvider services, JsonSerializerOptions json) =>
        Task.FromResult(CommandOutput.Write(Binding.Forge(services).Parse(line.Positionals[0]), json));
}
