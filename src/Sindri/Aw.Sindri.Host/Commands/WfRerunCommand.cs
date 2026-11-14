using Aw.Sindri.Core;
using Aw.Sindri.Features.RerunWorkflowRun;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Commands;

/// <summary>'sindri wf rerun &lt;run&gt; --from &lt;node&gt; [--input JSON] [--json]': a client of POST /sindri/workflow-runs/{run}/rerun. Starts the control plane first, like the other workflow writes. Exit 0 ok, 1 not running, 2 refused or error.</summary>
internal static class WfRerunCommand
{
    private const string Usage = "usage: sindri wf rerun <run> --from <node> [--input JSON] [--json]";

    internal static async Task<int> Run(string[] args, RecipeDesk desk, Output output, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        var (run, from, input, json, bad) = Parse(args);
        if (run is null || from is null || bad is not null)
        {
            return await output.Error(SindriErrors.InvalidInput.With((bad ?? "wf rerun needs a run id and --from <node>") + $" ({Usage})")).ConfigureAwait(false);
        }

        if (await desk.EnsureRunning(ct).ConfigureAwait(false) is { } down)
        {
            return await output.Error(down).ConfigureAwait(false);
        }

        var started = await desk.Client.Post<RerunWorkflowRunBody, RerunWorkflowRunResponse>("/sindri/workflow-runs/" + Uri.EscapeDataString(run) + "/rerun", new RerunWorkflowRunBody(from, input), ct).ConfigureAwait(false);
        if (started.IsError)
        {
            return await output.Error(started.Error).ConfigureAwait(false);
        }

        var answer = started.Value.Value;
        return json
            ? await output.Raw(started.Value.Body, 0).ConfigureAwait(false)
            : await output.Text($"{answer.Status} {answer.Run.Value} from {answer.Source.Value}: restored {string.Join(", ", answer.Restored)}; running again {string.Join(", ", answer.Rerun)}\n", 0).ConfigureAwait(false);
    }

    private static (string? Run, string? From, string? Input, bool Json, string? Bad) Parse(string[] args)
    {
        string? run = null;
        string? from = null;
        string? input = null;
        var json = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json":
                    json = true;
                    break;
                case "--from" when i + 1 < args.Length:
                    from = args[++i];
                    break;
                case "--input" when i + 1 < args.Length:
                    input = args[++i];
                    break;
                case "--from" or "--input":
                    return (run, from, input, json, $"'{args[i]}' needs a value");
                case var flag when flag.StartsWith('-'):
                    return (run, from, input, json, $"'{flag}' isn't an option");
                default:
                    if (run is not null)
                    {
                        return (run, from, input, json, $"'{args[i]}' is one argument too many");
                    }

                    run = args[i];
                    break;
            }
        }

        return (run, from, input, json, null);
    }
}
