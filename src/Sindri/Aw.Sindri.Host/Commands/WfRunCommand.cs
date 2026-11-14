using Aw.Sindri.Core;
using Aw.Sindri.Features.CancelWorkflowRun;
using Aw.Sindri.Features.RunWorkflow;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri wf run &lt;slug&gt; [--payload JSON] [--json]' and 'sindri wf cancel &lt;run&gt; [--json]': clients of POST /sindri/workflows/{slug}/runs and
/// POST /sindri/workflow-runs/{run}/cancel. Both start the control plane first, like the other workflow writes. Exit 0 ok, 1 not running, 2 refused or error.
/// </summary>
internal static class WfRunCommand
{
    private const string RunUsage = "usage: sindri wf run <slug> [--payload JSON] [--json]";
    private const string CancelUsage = "usage: sindri wf cancel <run> [--json]";

    internal static async Task<int> Run(string[] args, RecipeDesk desk, Output output, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        var (slug, payload, json, bad) = Parse(args);
        if (slug is null || bad is not null)
        {
            return await output.Error(SindriErrors.InvalidInput.With((bad ?? "wf run needs a workflow slug") + $" ({RunUsage})")).ConfigureAwait(false);
        }

        if (await desk.EnsureRunning(ct).ConfigureAwait(false) is { } down)
        {
            return await output.Error(down).ConfigureAwait(false);
        }

        var started = await desk.Client.Post<RunWorkflowBody, RunWorkflowResponse>("/sindri/workflows/" + Uri.EscapeDataString(slug) + "/runs", new RunWorkflowBody(payload, Source: "cli"), ct).ConfigureAwait(false);
        if (started.IsError)
        {
            return await output.Error(started.Error).ConfigureAwait(false);
        }

        if (json)
        {
            return await output.Raw(started.Value.Body, 0).ConfigureAwait(false);
        }

        var answer = started.Value.Value;
        var line = answer.Reason is null ? $"{answer.Status} {answer.Run.Value}" : $"{answer.Status} {answer.Run.Value}: {answer.Reason}";
        return await output.Text(line + "\n", 0).ConfigureAwait(false);
    }

    internal static async Task<int> Cancel(string[] args, RecipeDesk desk, Output output, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        var json = args.Contains("--json", StringComparer.Ordinal);
        if (args.Where(static a => a != "--json").ToList() is not [{ } run] || run.StartsWith('-'))
        {
            return await output.Error(SindriErrors.InvalidInput.With($"wf cancel needs one run id ({CancelUsage})")).ConfigureAwait(false);
        }

        if (await desk.EnsureRunning(ct).ConfigureAwait(false) is { } down)
        {
            return await output.Error(down).ConfigureAwait(false);
        }

        var cancelled = await desk.Client.Post<RunWorkflowBody, CancelWorkflowRunResponse>("/sindri/workflow-runs/" + Uri.EscapeDataString(run) + "/cancel", body: null, ct).ConfigureAwait(false);
        if (cancelled.IsError)
        {
            return await output.Error(cancelled.Error).ConfigureAwait(false);
        }

        return json
            ? await output.Raw(cancelled.Value.Body, 0).ConfigureAwait(false)
            : await output.Text($"{cancelled.Value.Value.Run.Value} cancel requested\n", 0).ConfigureAwait(false);
    }

    private static (string? Slug, string? Payload, bool Json, string? Bad) Parse(string[] args)
    {
        string? slug = null;
        string? payload = null;
        var json = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json":
                    json = true;
                    break;
                case "--payload" when i + 1 < args.Length:
                    payload = args[++i];
                    break;
                case "--payload":
                    return (slug, payload, json, "'--payload' needs a JSON object");
                case var flag when flag.StartsWith('-'):
                    return (slug, payload, json, $"'{flag}' isn't an option");
                default:
                    if (slug is not null)
                    {
                        return (slug, payload, json, $"'{args[i]}' is one argument too many");
                    }

                    slug = args[i];
                    break;
            }
        }

        return (slug, payload, json, null);
    }
}
