using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Reads the finished run a 'workflow_result' node asks for, over the workflow and run stores. It never starts a run. A run id must be one of that workflow's runs and must have ended (any final status);
/// 'latest' is the newest run (by start time) that succeeded, looked for in the workflow's 1,000 newest runs.
/// </summary>
/// <param name="workflows">The workflows.</param>
/// <param name="runs">The runs.</param>
internal sealed class GraphRunResults(IWorkflowStore workflows, IWorkflowRunStore runs) : IGraphResultPort
{
    private const int PageSize = 100;
    private const int Pages = 10;

    public async ValueTask<Result<GraphRunResult>> Read(WorkflowSlug workflow, WorkflowRunId? run, CancellationToken cancellationToken)
    {
        var listed = await workflows.List(includeArchived: false, cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        if (listed.Value.FirstOrDefault(r => r.Slug == workflow) is not { } record)
        {
            return SindriErrors.GraphNotFound.With($"no workflow '{workflow.Value}'; list the workflows to see what exists");
        }

        var found = run is { } id ? await ById(record, id, cancellationToken).ConfigureAwait(false) : await LatestSucceeded(record, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        return ToResult(found.Value);
    }

    private async ValueTask<Result<WorkflowRun>> ById(WorkflowRecord record, WorkflowRunId id, CancellationToken cancellationToken)
    {
        var row = await runs.Get(id, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            return string.Equals(row.Error.Code, SindriErrors.GraphRunNotFound.Code, StringComparison.Ordinal)
                ? SindriErrors.GraphResultUnavailable.With($"'{id.Value}' is not a run of '{record.Slug.Value}'")
                : row.Error;
        }

        if (string.Equals(row.Value.Mode, WorkflowRunMode.Test, StringComparison.Ordinal))
        {
            return SindriErrors.GraphResultUnavailable.With($"'{id.Value}' is a test run; 'workflow_result' reads real runs only (fix: name a run of '{record.Slug.Value}' that was not a test)");
        }

        if (row.Value.WorkflowId != record.Id)
        {
            return SindriErrors.GraphResultUnavailable.With($"'{id.Value}' is not a run of '{record.Slug.Value}'");
        }

        return Name(row.Value.Status) is null
            ? SindriErrors.GraphResultUnavailable.With($"'{id.Value}' has not finished yet ({row.Value.Status})")
            : row.Value;
    }

    private async ValueTask<Result<WorkflowRun>> LatestSucceeded(WorkflowRecord record, CancellationToken cancellationToken)
    {
        for (var page = 0; page < Pages; page++)
        {
            var recent = await runs.Recent(record.Id, page * PageSize, PageSize, cancellationToken).ConfigureAwait(false);
            if (recent.IsError)
            {
                return recent.Error;
            }

            if (recent.Value.FirstOrDefault(static r => r.Status == WorkflowRunStatus.Succeeded) is { } newest)
            {
                return newest;
            }

            if (recent.Value.Count < PageSize)
            {
                break;
            }
        }

        return SindriErrors.GraphResultUnavailable.With($"'{record.Slug.Value}' has no succeeded run to read (fix: run it, or name a finished run in 'run')");
    }

    private static GraphRunResult ToResult(WorkflowRun row) => new(row.Id, Name(row.Status) ?? "running", row.EndedAt, Outputs(row.Outputs));

    private static string? Name(WorkflowRunStatus status) => status switch
    {
        WorkflowRunStatus.Succeeded => "succeeded",
        WorkflowRunStatus.Failed => "failed",
        WorkflowRunStatus.Cancelled => "cancelled",
        WorkflowRunStatus.Queued or WorkflowRunStatus.Running or WorkflowRunStatus.Waiting => null,
        _ => null,
    };

    private static JsonNode? Outputs(string? text)
    {
        try
        {
            return text is null ? null : JsonNode.Parse(text);
        }
        catch (JsonException)   // boundary: a stored output that is not JSON is no output
        {
            return null;
        }
    }
}
