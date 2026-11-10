using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Features.GetWorkflow;
using Aw.Sindri.Features.GetWorkflowRun;
using Aw.Sindri.Features.ListWorkflowBlocks;
using Aw.Sindri.Features.ListWorkflowRuns;
using Aw.Sindri.Features.ListWorkflows;
using Aw.Sindri.Features.SearchWorkflowDocs;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri wf list', 'wf show &lt;slug&gt;', 'wf runs &lt;slug&gt;' and 'wf run-show &lt;run&gt;' (each with --json): clients of GET /sindri/workflows, /sindri/workflows/{slug}, /sindri/workflows/{slug}/runs and
/// /sindri/workflow-runs/{run}. They read the routes and never start the control plane. Exit 0 ok (an empty list included), 1 when Sindri isn't running, 2 on usage or any other error.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
internal sealed class WfReadVerbs(ControlPlaneClient client, Output output)
{
    internal const string ListUsage = "usage: sindri wf list [--json]";
    internal const string ShowUsage = "usage: sindri wf show <slug> [--json]";
    internal const string RunsUsage = "usage: sindri wf runs <slug> [--json]";
    internal const string BlocksUsage = "usage: sindri wf blocks [--json]";
    internal const string DocsUsage = "usage: sindri wf docs [<question words>] [--page <slug>] [--json]";
    internal const string RunShowUsage = "usage: sindri wf run-show <run> [--json]";

    internal async Task<int> List(string[] args, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        var bad = One(args, 0, "wf list", ListUsage);
        if (bad.IsError)
        {
            return await output.Error(bad.Error).ConfigureAwait(false);
        }

        var answer = await client.Get<ListWorkflowsResponse>("/sindri/workflows", ct).ConfigureAwait(false);
        return await Print(answer, args, WfReadText.List, ct).ConfigureAwait(false);
    }

    internal async Task<int> Show(string[] args, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        var slug = One(args, 1, "wf show", ShowUsage);
        if (slug.IsError)
        {
            return await output.Error(slug.Error).ConfigureAwait(false);
        }

        var answer = await client.Get<GetWorkflowResponse>("/sindri/workflows/" + Uri.EscapeDataString(slug.Value), ct).ConfigureAwait(false);
        return await Print(answer, args, WfReadText.Show, ct).ConfigureAwait(false);
    }

    internal async Task<int> Runs(string[] args, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        var slug = One(args, 1, "wf runs", RunsUsage);
        if (slug.IsError)
        {
            return await output.Error(slug.Error).ConfigureAwait(false);
        }

        var answer = await client.Get<ListWorkflowRunsResponse>("/sindri/workflows/" + Uri.EscapeDataString(slug.Value) + "/runs", ct).ConfigureAwait(false);
        return await Print(answer, args, WfReadText.Runs, ct).ConfigureAwait(false);
    }

    internal async Task<int> RunShow(string[] args, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        var run = One(args, 1, "wf run-show", RunShowUsage);
        if (run.IsError)
        {
            return await output.Error(run.Error).ConfigureAwait(false);
        }

        var answer = await client.Get<GetWorkflowRunResponse>("/sindri/workflow-runs/" + Uri.EscapeDataString(run.Value), ct).ConfigureAwait(false);
        return await Print(answer, args, WfReadText.Run, ct).ConfigureAwait(false);
    }

    internal async Task<int> Blocks(string[] args, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        var bad = One(args, 0, "wf blocks", BlocksUsage);
        if (bad.IsError)
        {
            return await output.Error(bad.Error).ConfigureAwait(false);
        }

        var answer = await client.Get<ListWorkflowBlocksResponse>("/sindri/workflows/blocks", ct).ConfigureAwait(false);
        return await Print(answer, args, WfReadText.Blocks, ct).ConfigureAwait(false);
    }

    internal async Task<int> Docs(string[] args, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        string? page = null;
        var words = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--json")
            {
                continue;
            }

            if (args[i] == "--page")
            {
                if (i + 1 >= args.Length)
                {
                    return await output.Error(SindriErrors.InvalidInput.With("'--page' needs a page slug (" + DocsUsage + ")")).ConfigureAwait(false);
                }

                page = args[++i];
            }
            else if (args[i].StartsWith('-'))
            {
                return await output.Error(SindriErrors.InvalidInput.With($"'{args[i]}' isn't an option of wf docs ({DocsUsage})")).ConfigureAwait(false);
            }
            else
            {
                words.Add(args[i]);
            }
        }

        var query = UrlQuery.Of([("question", words.Count == 0 ? null : string.Join(' ', words)), ("page", page)]);
        var answer = await client.Get<SearchWorkflowDocsResponse>("/sindri/workflows/docs" + query, ct).ConfigureAwait(false);
        return await Print(answer, args, WfReadText.Docs, ct).ConfigureAwait(false);
    }

    private async Task<int> Print<T>(Result<ApiAnswer<T>> answer, string[] args, Func<T, string> text, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        return args.Contains("--json", StringComparer.Ordinal)
            ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(text(answer.Value.Value), 0).ConfigureAwait(false);
    }

    /// <summary>The verb's positional arguments: exactly <paramref name="count"/> and the optional --json.</summary>
    private static Result<string> One(string[] args, int count, string verb, string usage)
    {
        var rest = args.Where(static a => a != "--json").ToList();
        var flag = rest.Find(static a => a.StartsWith('-'));
        if (flag is not null)
        {
            return SindriErrors.InvalidInput.With($"'{flag}' isn't an option of {verb} ({usage})");
        }

        return rest.Count == count
            ? count == 0 ? string.Empty : rest[0]
            : SindriErrors.InvalidInput.With($"{verb} takes {(count == 0 ? "no argument" : "one argument")} ({usage})");
    }
}
