using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Expressions;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Graph.Schedule;
using Aw.Sindri.Core.Graph.Validation;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.ActivateWorkflow;
using Aw.Sindri.Features.ApplyWorkflowOps;
using Aw.Sindri.Features.CreateWorkflow;
using Aw.Sindri.Features.ValidateWorkflow;
using Aw.Sindri.Features.WorkflowSchedule;
using Aw.Sindri.Host;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Temporalio.Worker;
using HostWorkflowTools = Aw.Sindri.Host.Mcp.WorkflowTools;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The run half of PR8b's acceptance story (issue 2453, part of 2258), next to the write half in Aw.Sindri.Tests' DriftCheckAcceptanceTests: a launched agent session makes the weekday-check
/// workflow through the workflow MCP tools (create, apply_ops, validate, activate), then the schedule service fires a tick and the run walks its steps on the time-skipping server,
/// over the real block types. Asserts who each ledger event names.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class DriftCheckRunAcceptanceTests
{
    private const string Summary = "the client has no field 'retryAfter' that the API returns";
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("drift-check");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // why: the tick that starts the run is the scheduler's, not the session's: it runs with no caller, so GraphMirror records the run's events as 'service:workflow' (envelope actor and payload By), not the launch.
    // The workflow.* writes made through the tools do name the launch. Both are asserted as recorded; a run the session's launch is meant to own would be a product decision, not a test change.
    [Fact]
    public async Task A_workflow_a_session_made_runs_on_its_tick_and_every_event_names_who_wrote_it()
    {
        await using var server = new SindriTimeSkippingServer();
        await server.InitializeAsync();
        await using var rig = new Rig(server);
        var actor = await rig.MakeAsSession();

        var runId = await rig.Tick();
        var run = await Final(rig.Runs, runId);

        // the steps ran: schedule -> prompt -> condition -> notify
        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        var nodes = (await rig.Runs.NodeRuns(runId, Token)).Value;
        Assert.Equal(["check", "gate", "tell", "weekdays"], nodes.Select(static n => n.NodeId.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.All(nodes, static n => Assert.Equal(NodeRunStatus.Succeeded, n.Status));
        var (node, title, body) = Assert.Single(rig.Notify.Posted);
        Assert.Equal(("tell", "Needs attention", Summary), (node, title, body));

        // who wrote what: wait for the run's closing row, which is appended just after the status
        var all = await WaitFor(rig.Ledger, EventTypes.WorkflowRunFinished);
        var writes = all.Where(static e => e.Envelope.Type.StartsWith("workflow.", StringComparison.Ordinal) && !e.Envelope.Type.StartsWith("workflow.run.", StringComparison.Ordinal) && !e.Envelope.Type.StartsWith("workflow.node.", StringComparison.Ordinal)).ToList();
        Assert.Equal([EventTypes.WorkflowCreated, EventTypes.WorkflowRevised, EventTypes.WorkflowActivated], writes.Select(static e => e.Envelope.Type), StringComparer.Ordinal);
        Assert.All(writes.Where(static e => e.Envelope.Type != EventTypes.WorkflowActivated), e => Assert.Equal((actor, actor), (e.Envelope.Actor, By(e))));
        Assert.Equal("human:cli", By(writes.Single(static e => e.Envelope.Type == EventTypes.WorkflowActivated)));

        var runEvents = all.Where(static e => e.Envelope.Type.StartsWith("workflow.run.", StringComparison.Ordinal) || e.Envelope.Type.StartsWith("workflow.node.", StringComparison.Ordinal)).ToList();
        Assert.Equal(EventTypes.WorkflowRunStarted, runEvents[0].Envelope.Type);
        Assert.Equal(EventTypes.WorkflowRunFinished, runEvents[^1].Envelope.Type);
        Assert.Equal(8, runEvents.Count(static e => e.Envelope.Type.StartsWith("workflow.node.", StringComparison.Ordinal)));
        Assert.All(runEvents, static e => Assert.Equal((GraphMirror.By, GraphMirror.By), (e.Envelope.Actor, By(e))));
        Assert.DoesNotContain(runEvents, e => e.Envelope.Actor == actor);
    }

    /// <summary>The stores, the real blocks, the starter and a Temporal worker; the worker starts before any caller is entered, so what its activities write cannot inherit the session's caller through the async context.</summary>
    private sealed class Rig : IAsyncDisposable
    {
        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-11-09T06:00:00Z", System.Globalization.CultureInfo.InvariantCulture));   // a Monday, an hour before the 07:00 tick
        private readonly SequentialIdGenerator _ids = new();
        private readonly AsyncLocalCallerContextAccessor _callers;
        private readonly InMemoryWorkflowStore _workflows;
        private readonly GraphBlockRegistry _registry;
        private readonly WorkflowChangeLog _log;
        private readonly GraphRunStarter _starter;
        private readonly TemporalWorker _worker;
        private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(Token);
        private readonly Task _loop;

        public Rig(SindriTimeSkippingServer server)
        {
            var probe = new FakeProcessProbe();
            Ledger = new InMemorySindriStore(_clock, probe);
            var held = new HeldLease();
            held.Hold(Ledger.Acquire(probe.Self(), Token).AsTask().GetAwaiter().GetResult().Value);
            _callers = new AsyncLocalCallerContextAccessor(_ids);
            var events = new EventFactory(EventScope.ControlPlane, _clock, _ids, AwJson.CreateOptions(SindriJsonContext.Default), _callers);
            _workflows = new InMemoryWorkflowStore(_clock);
            Runs = new InMemoryWorkflowRunStore(_clock);
            Notify = new RecordingNotify();
            _registry = GraphBlockRegistry.Standard(Notify, new ScriptedModel($$"""{"flagged":true,"summary":"{{Summary}}"}"""));
            _log = new WorkflowChangeLog(Ledger, held, events, _callers);
            var mirror = new GraphMirror(Ledger, held, events);
            _starter = new GraphRunStarter(Runs, new TemporalGraphRunLauncher(_ => ValueTask.FromResult(Results.Success(server.Environment.Client)), "drift-acceptance"), mirror, _clock, NullLogger<GraphRunStarter>.Instance);
            _worker = new TemporalWorker(server.Environment.Client, new TemporalWorkerOptions("drift-acceptance")
                .AddWorkflow<GraphWorkflow>()
                .AddWorkflow<GraphIterationWorkflow>()
                .AddAllActivities(new GraphRunActivities(Runs, _workflows, _starter, mirror))
                .AddAllActivities(new GraphBlockActivities(_registry, Runs, _workflows)));
            _loop = _worker.ExecuteAsync(_stop.Token);
        }

        public InMemorySindriStore Ledger { get; }

        public InMemoryWorkflowRunStore Runs { get; }

        public RecordingNotify Notify { get; }

        /// <summary>As a launched session: create from the template, edit by ops, validate, activate. Returns the session's actor.</summary>
        public async Task<string> MakeAsSession()
        {
            var launch = LaunchId.From("LCH-" + new string('0', 31) + "1");
            var actor = "session:launch:" + launch.Value;
            await using var services = Services();
            using var caller = _callers.Enter(new CallerContext(CallerClass.Session, Terminal: false, actor, _ids.New<CommandId>(), _ids.New<CorrelationId>(), Repo: null, launch));
            var made = await Call(services, HostWorkflowTools.Create, ("slug", _slug.Value), ("template", "weekday-check"));
            // why: the template's timezone is the box's; fixing it to UTC makes the tick 07:00Z on any machine
            var edited = await Call(services, HostWorkflowTools.ApplyOps, ("slug", _slug.Value), ("baseRevision", 1), ("ops", new object[]
            {
                new { op = "set_config", node = "weekdays", path = "timezone", value = "UTC" },
                new { op = "set_config", node = "check", path = "prompt", value = "Compare the API contract with the client's types. Set flagged to true when a field is missing or wrong, and summarize why." },
            }));
            var valid = await Call(services, HostWorkflowTools.Validate, ("slug", _slug.Value));
            // why: turning it on is the owner's (workflows page spec §8): the session asks and the owner presses Activate
            using var owner = _callers.Enter(new CallerContext(CallerClass.Human, Terminal: true, "human:cli", _ids.New<CommandId>(), _ids.New<CorrelationId>(), Repo: null));
            var on = await Call(services, HostWorkflowTools.Activate, ("slug", _slug.Value));
            Assert.All([made, edited, valid, on], static r => Assert.True(r.IsError != true, r.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().FirstOrDefault()?.Text));
            return actor;
        }

        /// <summary>07:00 has passed: the schedule service, with no caller as the host's scheduler runs it, starts the run. Returns its id.</summary>
        public async Task<WorkflowRunId> Tick()
        {
            _clock.SetUtcNow(DateTimeOffset.Parse("2026-11-09T07:00:30Z", System.Globalization.CultureInfo.InvariantCulture));
            var holder = new LeaseHolder(HostId.From("test-host"), 1, _clock.GetUtcNow(), BootId.From("test-boot"));
            var grant = new LeaseGrant(holder, 1, _clock.GetUtcNow().AddMinutes(1));
            var tick = await new WorkflowScheduleService(_workflows, Runs, _starter, _clock).Run(new ScheduledRun(grant, _clock.GetUtcNow()), Token);
            Assert.True(tick.IsSuccess);
            Assert.False(tick.Value.Idle);
            return WorkflowRunId.Of(_slug, 2, checked((int)(DateTimeOffset.Parse("2026-11-09T07:00:00Z", System.Globalization.CultureInfo.InvariantCulture).ToUnixTimeSeconds() / 60)));
        }

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync();
            try
            {
                await _loop;
            }
            catch (OperationCanceledException)
            {
                // the worker stopped
            }

            _worker.Dispose();
            _stop.Dispose();
        }

        private ServiceProvider Services()
        {
            var checker = new Checker(_registry);
            return new ServiceCollection()
                .AddSingleton(AwJson.CreateOptions(SindriJsonContext.Default))
                .AddSingleton<ICallerContextAccessor>(_callers)
                .AddSingleton<IService<CreateWorkflowRequest, CreateWorkflowResponse>>(new CreateWorkflowService(_workflows, checker, _ids, _log, new OpenGate()))
                .AddSingleton<IService<ApplyWorkflowOpsRequest, ApplyWorkflowOpsResponse>>(new ApplyWorkflowOpsService(_workflows, checker, _log, new OpenGate()))
                .AddSingleton<IService<ValidateWorkflowRequest, ValidateWorkflowResponse>>(new ValidateWorkflowService(_workflows, checker))
                .AddSingleton<IService<ActivateWorkflowRequest, ActivateWorkflowResponse>>(new ActivateWorkflowService(_workflows, checker, _log, _callers))
                .BuildServiceProvider();
        }
    }

    private static string By(LedgerEvent e) => JsonNode.Parse(e.Envelope.Payload)!.AsObject().Single(static p => string.Equals(p.Key, "by", StringComparison.OrdinalIgnoreCase)).Value!.GetValue<string>();

    private static async Task<WorkflowRun> Final(InMemoryWorkflowRunStore runs, WorkflowRunId id)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(Token);
        limit.CancelAfter(TimeSpan.FromSeconds(30));
        while (true)
        {
            var run = (await runs.Get(id, limit.Token)).Value;
            if (run.Status is WorkflowRunStatus.Succeeded or WorkflowRunStatus.Failed or WorkflowRunStatus.Cancelled)
            {
                return run;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), limit.Token);
        }
    }

    private static async Task<IReadOnlyList<LedgerEvent>> WaitFor(InMemorySindriStore ledger, string type)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(Token);
        limit.CancelAfter(TimeSpan.FromSeconds(30));
        while (true)
        {
            var read = (await ledger.ReadAfter(0, 1000, limit.Token)).Value;
            if (read.Any(e => e.Envelope.Type == type))
            {
                return read;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), limit.Token);
        }
    }

    private static ValueTask<ModelContextProtocol.Protocol.CallToolResult> Call(IServiceProvider services, string tool, params (string Name, object Value)[] pairs) =>
        HostWorkflowTools.Call(tool, services, pairs.ToDictionary(static p => p.Name, static p => JsonSerializer.SerializeToElement(p.Value), StringComparer.Ordinal), Token);

    private sealed class Checker(GraphBlockRegistry registry) : IWorkflowChecker
    {
        public ValueTask<GraphValidator> Validator(CancellationToken cancellationToken) => ValueTask.FromResult(new GraphValidator(new GraphValidationContext(
            registry.Types,
            new KnownWorld(),
            (cron, zone) => CronExpression.TryParse(cron, zone, out _, out var problem) ? null : problem,
            text => ConditionExpression.TryParse(text, out var expression, out var error) ? new ConditionParse(Error: null, expression!.References) : new ConditionParse(error, []))));
    }

    private sealed class KnownWorld : IGraphWorld
    {
        public GraphLookup Repo(string name) => GraphLookup.Known;

        public GraphLookup Connection(string name) => GraphLookup.Known;

        public GraphLookup Memory(string name) => GraphLookup.Known;
    }

    internal sealed class RecordingNotify : INotifyPort
    {
        public List<(string Node, string Title, string? Body)> Posted { get; } = [];

        public ValueTask<Result<Unit>> Post(WorkflowRunId runId, NodeId node, string title, string? body, CancellationToken cancellationToken)
        {
            Posted.Add((node.Value, title, body));
            return ValueTask.FromResult<Result<Unit>>(Unit.Value);
        }
    }

    private sealed class OpenGate : Core.Commands.ICommandGate
    {
        public ValueTask<IDisposable?> Enter(CancellationToken cancellationToken) => ValueTask.FromResult<IDisposable?>(new Hold());

        private sealed class Hold : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private sealed class ScriptedModel(string answer) : IGraphModelPort
    {
        public string Untrusted(string source, string text) => $"<untrusted>{text}</untrusted>";

        public ValueTask<Result<string>> Ask(string prompt, CancellationToken cancellationToken) => ValueTask.FromResult<Result<string>>(answer);
    }
}
