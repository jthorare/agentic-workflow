using System.Globalization;
using Aw.Sindri.Core;
using Aw.Sindri.Features.AgentNodeControl;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri wf takeover|handback &lt;run&gt; &lt;node&gt; [--item N] [--json]': clients of POST /sindri/workflow-runs/{run}/nodes/{node}/takeover and /handback (#2472). Like 'wf cancel' they start the control plane first.
/// Exit 0 ok, 1 not running, 2 refused or bad arguments.
/// </summary>
internal static class WfAgentNodeCommand
{
    private const string TakeOverUsage = "usage: sindri wf takeover <run> <node> [--item <index>] [--json]";
    private const string HandBackUsage = "usage: sindri wf handback <run> <node> [--item <index>] [--json]";

    internal static Task<int> TakeOver(string[] args, RecipeDesk desk, Output output, CancellationToken ct) => Send(new Verb("takeover", "taken over", TakeOverUsage), args, desk, output, ct);

    internal static Task<int> HandBack(string[] args, RecipeDesk desk, Output output, CancellationToken ct) => Send(new Verb("handback", "handed back", HandBackUsage), args, desk, output, ct);

    private static async Task<int> Send(Verb what, string[] args, RecipeDesk desk, Output output, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        var (verb, done, usage) = what;
        var (run, node, item, json, bad) = Parse(args);
        if (run is null || node is null || bad is not null)
        {
            return await output.Error(SindriErrors.InvalidInput.With((bad ?? $"wf {verb} needs a run id and a node id") + $" ({usage})")).ConfigureAwait(false);
        }

        if (await desk.EnsureRunning(ct).ConfigureAwait(false) is { } down)
        {
            return await output.Error(down).ConfigureAwait(false);
        }

        var route = "/sindri/workflow-runs/" + Uri.EscapeDataString(run) + "/nodes/" + Uri.EscapeDataString(node) + "/" + verb;
        var answered = await desk.Client.Post<AgentNodeBody, AgentNodeControlled>(route, new AgentNodeBody(item), ct).ConfigureAwait(false);
        if (answered.IsError)
        {
            return await output.Error(answered.Error).ConfigureAwait(false);
        }

        return json
            ? await output.Raw(answered.Value.Body, 0).ConfigureAwait(false)
            : await output.Text($"{answered.Value.Value.Run.Value} {answered.Value.Value.Node.Value} {done}\n", 0).ConfigureAwait(false);
    }

    private static (string? Run, string? Node, int Item, bool Json, string? Bad) Parse(string[] args)
    {
        string? run = null;
        string? node = null;
        var item = 0;
        var json = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json":
                    json = true;
                    break;
                case "--item" when i + 1 < args.Length && int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out item):
                    i++;
                    break;
                case "--item":
                    return (run, node, item, json, "'--item' needs the item's position (0 or more)");
                case var flag when flag.StartsWith('-'):
                    return (run, node, item, json, $"'{flag}' isn't an option");
                default:
                    if (run is null)
                    {
                        run = args[i];
                    }
                    else if (node is null)
                    {
                        node = args[i];
                    }
                    else
                    {
                        return (run, node, item, json, $"'{args[i]}' is one argument too many");
                    }

                    break;
            }
        }

        return (run, node, item, json, null);
    }

    private sealed record Verb(string Route, string Done, string Usage);
}
