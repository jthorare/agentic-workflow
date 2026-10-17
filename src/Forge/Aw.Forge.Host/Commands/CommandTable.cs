using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Forge.Host.Commands;

/// <summary>Command name to handler. Each handler binds its arguments, calls the forge port and maps the result to an exit code.</summary>
internal static class CommandTable
{
    private static readonly Dictionary<string, (CommandSpec Spec, Func<CommandLine, IServiceProvider, JsonSerializerOptions, Task<int>> Run)> _commands =
        new(StringComparer.Ordinal)
        {
            [ParseCommand.Name] = (ParseCommand.Spec, ParseCommand.Run),
            [OpenCommand.Name] = (OpenCommand.Spec, OpenCommand.Run),
            [PullCommand.Name] = (PullCommand.Spec, PullCommand.Run),
            [ThreadsCommand.Name] = (ThreadsCommand.Spec, ThreadsCommand.Run),
            [SeedThreadCommand.Name] = (SeedThreadCommand.Spec, SeedThreadCommand.Run),
            [SeedChecksCommand.Name] = (SeedChecksCommand.Spec, SeedChecksCommand.Run),
            [SeedAdvanceCommand.Name] = (SeedAdvanceCommand.Spec, SeedAdvanceCommand.Run),
        };

    internal static string Usage { get; } =
        "aw-forge-fake <command> [options]\n" + string.Join('\n', _commands.Values.Select(static c => "  " + c.Spec.Usage));

    internal static async Task<int> Run(string[] args, IServiceProvider provider, JsonSerializerOptions json)
    {
        var take = args.Length > 1 && args[0] == "seed" ? 2 : 1;
        var name = string.Join(' ', args.Take(take));
        if (args.Length == 0 || !_commands.TryGetValue(name, out var command))
        {
            return CommandOutput.Usage(args.Length == 0 ? "no command" : $"unknown command '{name}'", Usage);
        }

        if (CommandLine.Parse(args[take..], command.Spec, out var problem) is not { } line)
        {
            return CommandOutput.Usage(problem, command.Spec.Usage);
        }

        var scope = provider.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            return await command.Run(line, scope.ServiceProvider, json).ConfigureAwait(false);
        }
    }
}
