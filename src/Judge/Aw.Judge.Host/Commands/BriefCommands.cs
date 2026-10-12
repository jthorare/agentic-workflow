using System.Text.Json;

namespace Aw.Judge.Host.Commands;

/// <summary>'judge brief &lt;sub&gt; …': routes the brief subcommands, checking each one's argument count (a wrong count is usage, 64).</summary>
internal static class BriefCommands
{
    internal const string Name = "brief";
    internal const string Synopsis = "brief save|get|map-by-name|map-by-dispatch|set-agent-id …";

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json)
    {
        if (args.Count == 0)
        {
            return CommandOutput.Usage(Synopsis);
        }

        var a = args.Skip(1).ToArray();
        return args[0] switch
        {
            SaveBriefCommand.Name when a.Length == 0 => await SaveBriefCommand.Run(services, json).ConfigureAwait(false),
            SaveBriefCommand.Name => CommandOutput.Usage("brief save < input.json"),
            GetBriefCommand.Name when a.Length == 1 => await GetBriefCommand.Run(a[0], services, json).ConfigureAwait(false),
            GetBriefCommand.Name => CommandOutput.Usage("brief get <tool-use-id>"),
            MapBriefByNameCommand.Name when a.Length == 1 => await MapBriefByNameCommand.Run(a[0], services).ConfigureAwait(false),
            MapBriefByNameCommand.Name => CommandOutput.Usage("brief map-by-name <name>"),
            MapBriefByDispatchCommand.Name when a.Length == 3 => await MapBriefByDispatchCommand.Run(a[0], a[1], a[2], services).ConfigureAwait(false),
            MapBriefByDispatchCommand.Name => CommandOutput.Usage("brief map-by-dispatch <session-id> <prompt-id> <subagent-type>"),
            SetBriefAgentIdCommand.Name when a.Length == 2 => await SetBriefAgentIdCommand.Run(a[0], a[1], services).ConfigureAwait(false),
            SetBriefAgentIdCommand.Name => CommandOutput.Usage("brief set-agent-id <tool-use-id> <agent-id>"),
            _ => CommandOutput.Usage(Synopsis, $"unknown subcommand '{args[0]}'"),
        };
    }
}
