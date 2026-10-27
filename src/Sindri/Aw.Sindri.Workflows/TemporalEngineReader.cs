using System.Diagnostics;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.EngineReads;
using Aw.Sindri.Core.Workflow.Engine;
using Google.Protobuf;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.WorkflowService.V1;
using Temporalio.Client;
using Temporalio.Exceptions;
using ApiExecution = Temporalio.Api.Common.V1.WorkflowExecution;
using ApiTaskQueue = Temporalio.Api.TaskQueue.V1.TaskQueue;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// <see cref="IEngineReader"/> over the Temporal .NET SDK's client (API spec §3.8, P13, E-30): the namespace and the task queue's description for the server and the worker, the visibility
/// list and each instance's description and newest events for the instances, and the reverse history for one instance's events. The one place besides the worker that holds a Temporal
/// client; the app, the CLI, the UI and the MCP server learn nothing of Temporal's address (ARCH-ENGINE-001 in the architecture tests keeps it so). A server that doesn't answer is SND-ENGINE-001.
/// </summary>
/// <param name="frontend">Finds and connects to the dev server serve keeps.</param>
/// <param name="settings">The namespace, the task queue and the home the server's database is in.</param>
/// <param name="endpoints">control.json, for the dev server's process and ports.</param>
/// <param name="lag">How far behind the event bridge is.</param>
/// <param name="clock">The clock of the worker's last poll and the server's uptime.</param>
internal sealed class TemporalEngineReader(TemporalFrontend frontend, TemporalSettings settings, IEndpointFile endpoints, EngineMirrorLag lag, TimeProvider clock) : IEngineReader
{
    private const string Running = "WorkflowType = 'RecipeWorkflow' AND ExecutionStatus = 'Running'";

    /// <summary>The deadline of each Temporal RPC an engine read makes: past it the server counts as not answering.</summary>
    internal static readonly TimeSpan Deadline = TimeSpan.FromSeconds(60);

    private const int Concurrency = 8;

    private static readonly TimeSpan _polling = TimeSpan.FromMinutes(1);

    public ValueTask<Result<EngineServer>> Server(CancellationToken cancellationToken) => Call(client => ServerFacts(client, cancellationToken), cancellationToken);

    public ValueTask<Result<EngineWorker>> Worker(CancellationToken cancellationToken) => Call(client => WorkerFacts(client, cancellationToken), cancellationToken);

    public ValueTask<Result<EngineMirror>> Mirror(CancellationToken cancellationToken) => lag.Read(cancellationToken);

    public ValueTask<Result<IReadOnlyList<EngineInstanceFacts>>> Instances(CancellationToken cancellationToken) => Call(client => InstanceFacts(client, cancellationToken), cancellationToken);

    public ValueTask<Result<EngineHistoryFacts>> History(string reference, int page, int pageSize, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return Call(client => HistoryFacts(client, reference, page, pageSize, cancellationToken), cancellationToken);
    }

    /// <summary>Connects, runs <paramref name="read"/>, and turns a frontend that doesn't answer into SND-ENGINE-001.</summary>
    private async ValueTask<Result<T>> Call<T>(Func<ITemporalClient, Task<Result<T>>> read, CancellationToken cancellationToken)
    {
        var client = await frontend.Connect(flag: null, cancellationToken).ConfigureAwait(false);
        if (client.IsError)
        {
            return SindriErrors.EngineUnavailable.With(client.Error.Message);
        }

        try
        {
            return await read(client.Value).ConfigureAwait(false);
        }
        catch (Exception e) when (e is RpcException or TimeoutException or InvalidOperationException || (e is OperationCanceledException && !cancellationToken.IsCancellationRequested)) // boundary: a frontend that went away, a namespace that isn't serve's, a deadline, a closed connection
        {
            return SindriErrors.EngineUnavailable.With($"Temporal didn't answer: {e.Message} (fix: sindri doctor)");
        }
    }

    private async Task<Result<EngineServer>> ServerFacts(ITemporalClient client, CancellationToken cancellationToken)
    {
        _ = await TemporalReads.Run(rpc => client.WorkflowService.DescribeNamespaceAsync(new DescribeNamespaceRequest { Namespace = settings.Namespace }, rpc), cancellationToken, Deadline).ConfigureAwait(false);
        var recorded = await endpoints.Read(cancellationToken).ConfigureAwait(false);
        var temporal = recorded.IsSuccess ? recorded.Value?.Temporal : null;
        var database = new FileInfo(Path.Combine(settings.Home, SindriTemporal.DatabaseFile));
        return new EngineServer("temporal-dev", settings.Namespace, UpSeconds(temporal), database.Exists ? database.Length : 0,
            temporal is { UiPort: > 0 } ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"http://127.0.0.1:{temporal.UiPort}") : null);
    }

    /// <summary>How long the dev server process has run; 0 when it can't be told (no pid recorded, or the process is gone).</summary>
    private long UpSeconds(TemporalEndpoint? temporal)
    {
        if (temporal is null)
        {
            return 0;
        }

        try
        {
            using var process = Process.GetProcessById(temporal.Pid);
            return Math.Max(0, (long)(clock.GetUtcNow() - new DateTimeOffset(process.StartTime)).TotalSeconds);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) // boundary: a process that exited is not an error here
        {
            return 0;
        }
    }

    private async Task<Result<EngineWorker>> WorkerFacts(ITemporalClient client, CancellationToken cancellationToken)
    {
        var queue = new ApiTaskQueue { Name = settings.TaskQueue, Kind = TaskQueueKind.Normal };
        var request = new DescribeTaskQueueRequest { Namespace = settings.Namespace, TaskQueue = queue, TaskQueueType = TaskQueueType.Workflow, ReportStats = true };
        var described = await TemporalReads.Run(rpc => client.WorkflowService.DescribeTaskQueueAsync(request, rpc), cancellationToken, Deadline).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        var pollers = described.Pollers.Select(static p => (p.Identity, At: p.LastAccessTime.ToDateTimeOffset())).OrderByDescending(static p => p.At).ToList();
        var recent = pollers.Where(p => now - p.At <= _polling).ToList();
        return new EngineWorker(recent.Select(static p => p.Identity).Distinct(StringComparer.Ordinal).Count(), pollers.Count == 0 ? null : pollers[0].Identity, settings.TaskQueue,
            recent.Count > 0, ActivitiesRunning: 0, described.Stats?.ApproximateBacklogCount ?? 0, pollers.Count == 0 ? null : Math.Max(0, (long)(now - pollers[0].At).TotalSeconds));
    }

    private async Task<Result<IReadOnlyList<EngineInstanceFacts>>> InstanceFacts(ITemporalClient client, CancellationToken cancellationToken)
    {
        var executions = await TemporalReads.List(client, Running, cancellationToken, Deadline).ConfigureAwait(false);
        using var gate = new SemaphoreSlim(Concurrency);
        var reads = executions.Select(async execution =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await EngineInstanceScan.Read(client, settings, execution, cancellationToken).ConfigureAwait(false);
            }
            catch (RpcException e) when (e.Code == RpcException.StatusCode.NotFound) // boundary: it ended between the list and the read
            {
                return null;
            }
            finally
            {
                _ = gate.Release();
            }
        });
        var facts = await Task.WhenAll(reads).ConfigureAwait(false);
        return Results.Success<IReadOnlyList<EngineInstanceFacts>>([.. facts.OfType<EngineInstanceFacts>().OrderBy(static i => i.Address.Value, StringComparer.Ordinal)]);
    }

    private async Task<Result<EngineHistoryFacts>> HistoryFacts(ITemporalClient client, string reference, int page, int pageSize, CancellationToken cancellationToken)
    {
        var resolved = await Resolve(client, reference, cancellationToken).ConfigureAwait(false);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        try
        {
            var described = await TemporalReads.Run(rpc => client.GetWorkflowHandle(resolved.Value.Value).DescribeAsync(new() { Rpc = rpc }), cancellationToken, Deadline).ConfigureAwait(false);
            var events = await Page(client, resolved.Value, page, pageSize, cancellationToken).ConfigureAwait(false);
            return new EngineHistoryFacts(resolved.Value, described.RunId ?? string.Empty, [.. events.Select(EngineHistoryEvents.Facts)], described.HistoryLength);
        }
        catch (RpcException e) when (e.Code == RpcException.StatusCode.NotFound && !TemporalUpdates.NoNamespace(e)) // boundary: a retention edge, or an address that never ran
        {
            return SindriErrors.EngineNotFound.With($"Temporal has no execution for '{reference}'");
        }
    }

    /// <summary>The events of page <paramref name="page"/> of the reverse history (newest first): each page is fetched in turn with the continuation the one before returned.</summary>
    private async Task<List<Temporalio.Api.History.V1.HistoryEvent>> Page(ITemporalClient client, InstanceAddress address, int page, int pageSize, CancellationToken cancellationToken)
    {
        var continuation = ByteString.Empty;
        for (var number = 1; ; number++)
        {
            var request = new GetWorkflowExecutionHistoryReverseRequest { Namespace = settings.Namespace, Execution = new ApiExecution { WorkflowId = address.Value }, MaximumPageSize = pageSize, NextPageToken = continuation };
            var response = await TemporalReads.Run(rpc => client.WorkflowService.GetWorkflowExecutionHistoryReverseAsync(request, rpc), cancellationToken, Deadline).ConfigureAwait(false);
            if (number == page)
            {
                return [.. response.History.Events];
            }

            continuation = response.NextPageToken;
            if (continuation.IsEmpty)
            {
                return [];   // past the oldest event
            }
        }
    }

    /// <summary>The address a reference names: an address as it is, a 'WFI-' id through the instances Temporal lists.</summary>
    private static async Task<Result<InstanceAddress>> Resolve(ITemporalClient client, string reference, CancellationToken cancellationToken)
    {
        if (WorkflowInstanceId.TryFrom(reference, out var id))   // first: 'WFI-…' has an item key's shape too
        {
            var match = await TemporalReads.Find(client, "WorkflowType = 'RecipeWorkflow'", e => InstanceAddress.TryFrom(e.Id, out var address) && WorkflowInstanceId.Of(address) == id, cancellationToken, Deadline).ConfigureAwait(false);
            return match is null ? SindriErrors.EngineNotFound.With($"Temporal has no execution with the id {reference}") : InstanceAddress.From(match.Id);
        }

        return InstanceAddress.TryFrom(reference, out var named) ? named : SindriErrors.EngineNotFound.With($"'{reference}' is neither an instance id nor an address");
    }
}
