using System.Text.Json;

namespace Aw.Judge.Host.Commands;

/// <summary>'judge config get|set …': routes the config subcommands (a wrong argument count is usage, 64).</summary>
internal static class ConfigCommands
{
    internal const string Name = "config";
    internal const string Synopsis = "config get|set …";

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json)
    {
        if (args.Count == 0)
        {
            return CommandOutput.Usage(Synopsis);
        }

        return args[0] switch
        {
            GetConfigCommand.Name when args.Count == 1 => await GetConfigCommand.Run(services, json).ConfigureAwait(false),
            GetConfigCommand.Name => CommandOutput.Usage("config get"),
            SetConfigCommand.Name => await SetConfigCommand.Run([.. args.Skip(1)], services, json).ConfigureAwait(false),
            _ => CommandOutput.Usage(Synopsis, $"unknown subcommand '{args[0]}'"),
        };
    }
}
