using Aw.Sindri.Core;
using Aw.Sindri.Features.MarkWorkflowRunSeen;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Commands;

/// <summary>'sindri wf seen &lt;run&gt; [--json]': a client of POST /sindri/workflow-runs/{run}/seen. It marks a failed run seen, so its Home row clears. Exit 0 ok, 1 not running, 2 refused or unknown.</summary>
internal static class WfSeenCommand
{
    private const string Usage = "usage: sindri wf seen <run> [--json]";

    internal static async Task<int> Run(string[] args, RecipeDesk desk, Output output, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        var json = args.Contains("--json", StringComparer.Ordinal);
        if (args.Where(static a => a != "--json").ToList() is not [{ } run] || run.StartsWith('-'))
        {
            return await output.Error(SindriErrors.InvalidInput.With($"wf seen needs one run id ({Usage})")).ConfigureAwait(false);
        }

        if (await desk.EnsureRunning(ct).ConfigureAwait(false) is { } down)
        {
            return await output.Error(down).ConfigureAwait(false);
        }

        var seen = await desk.Client.Post<RunWorkflowBody, MarkWorkflowRunSeenResponse>("/sindri/workflow-runs/" + Uri.EscapeDataString(run) + "/seen", body: null, ct).ConfigureAwait(false);
        if (seen.IsError)
        {
            return await output.Error(seen.Error).ConfigureAwait(false);
        }

        return json
            ? await output.Raw(seen.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(seen.Value.Value.Marked ? $"{seen.Value.Value.Run.Value} marked seen\n" : $"{seen.Value.Value.Run.Value} is not a failed run: nothing to mark\n", 0).ConfigureAwait(false);
    }
}
