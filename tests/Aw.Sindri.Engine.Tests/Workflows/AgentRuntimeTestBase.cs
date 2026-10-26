using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Temporalio.Client;
using Temporalio.Testing;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The rig the agent runtime tests share (engine P8 U3): the composed host with FakeLauncher, FakeAgentSessions and FakeRunDiff, and the recipes they run.</summary>
[Collection(nameof(WallClockAlone))]
public abstract class AgentRuntimeTestBase : IDisposable
{
    internal const string RecipeName = "workflow.dev-agent";
    internal const string Repo = "/work/app";
    internal static readonly ItemInfo _item = new("T-1", "ITM-00000000000000000000000000000007", "app");
    internal readonly WorkflowsHarness _harness = new();
    internal readonly FakeLauncher _launcher = new();
    internal readonly FakeAgentSessions _sessions = new();
    internal readonly FakeRunDiff _diff = new();
    internal readonly FakeStepOutputs _outputs = new();   // the output folders of the run directories (issue #1000)
    internal string _root = RecipeName;   // the recipe a rig starts; a test that wants a parent over a child sets it and _also before Rig
    internal string[] _also = [];
    internal Dictionary<string, string> _more = new(StringComparer.Ordinal);   // more text inputs a root recipe declares, by key
    internal string? _factRepo;   // the repo key fact:item names, when a test wants one the profile doesn't list
    internal ILauncher? _launcherSeen;   // what the host sees as the launcher when a test wraps the fake
    internal IRunDiff? _diffSeen;

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _harness.Dispose();
        }
    }

    internal static Task<CommandResult> Cancel(WorkflowHandle<RecipeWorkflow, RecipeResult> handle, char nonce) =>
        handle.ExecuteUpdateAsync(wf => wf.CancelAsync(new CancelRequested("human:cli", CancelOrigin.Command, new CommandStamp(CommandNonce.From(new string(nonce, 32)), new string('1', 64)), Sequence: null, Parent: null)), new WorkflowUpdateOptions(new string(nonce, 32)));

    internal static StepCompleted Report(RunToken run)
    {
        var output = new FactValue("fact:sha256:" + new string('e', 64), new string('e', 64), HeadSha: null);
        return new StepCompleted("agent:session", run, OutcomeName.From("done"), new Dictionary<FactKey, FactValue> { [FactKey.From("fact:resolution")] = output }, Tokens: 0, StepRuntime.Agent, new string('f', 64));
    }

    internal async Task Launched(CancellationToken ct)
    {
        await WaitFor(() => _launcher.Requests.Count > 0, ct);
        await Task.Delay(TimeSpan.FromSeconds(1), ct);
    }

    internal static async Task WaitFor(Func<bool> condition, CancellationToken ct, int seconds = 30)
    {
        var deadline = RealTime.NowDateTime.AddSeconds(seconds);
        while (!condition() && RealTime.NowDateTime < deadline)
        {
            await Task.Delay(50, ct);
        }

        Assert.True(condition());
    }

    internal Task<AgentRig> Rig(CancellationToken ct, WorkflowEnvironment? environment = null) => AgentRig.Start(this, environment, ct);

    /// <summary>The real launcher's answer to a launch id whose session is already live and was not started by this process: that session, a Skipped viewer, no chosen id (#295 I28).</summary>
    internal sealed class SkippedOnRelaunch(FakeLauncher inner) : ILauncher
    {
        public async ValueTask<LaunchResult> Launch(LaunchRequest request, CancellationToken cancellationToken)
        {
            var first = inner.Requests.All(r => r.Launch != request.Launch);
            var result = await inner.Launch(request, cancellationToken);
            return first || result is not StartedLaunch started ? result : started with { Viewer = ViewerOutcome.Skipped, ChosenSession = null };
        }

        public ValueTask<Result<Unit>> Stop(LaunchId launch, CancellationToken cancellationToken) => inner.Stop(launch, cancellationToken);

        public ValueTask<LaunchProbed> Probe(CancellationToken cancellationToken) => inner.Probe(cancellationToken);
    }

    /// <summary>A launcher whose host never starts a session: every launch fails with <paramref name="error"/>, so the engine's retries run out.</summary>
    internal sealed class FailsEveryLaunch(FakeLauncher inner, Error error) : ILauncher
    {
        public ValueTask<LaunchResult> Launch(LaunchRequest request, CancellationToken cancellationToken) => ValueTask.FromResult<LaunchResult>(new FailedLaunch(error, LaunchStage.Host));

        public ValueTask<Result<Unit>> Stop(LaunchId launch, CancellationToken cancellationToken) => inner.Stop(launch, cancellationToken);

        public ValueTask<LaunchProbed> Probe(CancellationToken cancellationToken) => inner.Probe(cancellationToken);
    }

    /// <summary>
    /// Ends the session of each of the Step's three runs (the first try and the engine's two retries), once each has launched, so the Step runs out its retries.
    /// <paramref name="before"/> runs for a launch just before its session ends.
    /// </summary>
    internal async Task EndEveryRun(SessionEndReason reason, int? exitCode, CancellationToken ct, Action<LaunchId>? before = null)
    {
        for (var n = 1; n <= 3; n++)
        {
            await WaitFor(() => _launcher.Requests.Select(static r => r.Launch).Distinct().Skip(n - 1).Any(), ct);
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
            var launch = _launcher.Requests.Select(static r => r.Launch).Distinct().ElementAt(n - 1);
            before?.Invoke(launch);
            _sessions.End(launch, reason, exitCode);
        }
    }

    /// <summary>A run diff whose first read of HEAD throws, as a worker that dies mid-activity would: the launch activity fails once and is retried.</summary>
    internal sealed class FailsOnce(FakeRunDiff inner) : IRunDiff
    {
        private int _reads;

        public ValueTask<Result<string>> Head(string worktree, CancellationToken cancellationToken) =>
            Interlocked.Increment(ref _reads) == 1 ? throw new InvalidOperationException("the worker died") : inner.Head(worktree, cancellationToken);

        public ValueTask<Result<bool>> IsEmpty(string worktree, string startHead, CancellationToken cancellationToken) => inner.IsEmpty(worktree, startHead, cancellationToken);

        public ValueTask<Result<IReadOnlyList<string>>> ChangedPaths(string worktree, string baseCommit, CancellationToken cancellationToken) => inner.ChangedPaths(worktree, baseCommit, cancellationToken);
    }

    internal sealed record ItemInfo(string Key, string Id, string Repo);

    internal static readonly string _recipe = string.Join('\n',
        "schema: 1",
        "id: workflow.dev-agent",
        "version: 1",
        "description: \"A Step an agent session runs (engine P8 interpreter tests).\"",
        "inputs:",
        "  - { key: fact:item, label: \"Item\", type: item, kind: item }",
        "  - { key: fact:issue, label: \"Issue\", type: text, kind: text }",
        "initial: write",
        "states:",
        "  write:",
        "    kind: active",
        "    step: step.report.write",
        "    budget: { tokens: 1000 }",
        "    timeout: { after: 1m, then: park }",
        "    next: \"Write up the resolution for {item}.\"",
        "  done: { kind: terminal, outcome: resolved }",
        "transitions:",
        "  - { from: write, to: done, on: step.completed, when: { outcome: done }, produces: [fact:resolution] }");

    internal static readonly string _parentRecipe = string.Join('\n',
        "schema: 1",
        "id: workflow.dev-agent-parent",
        "version: 1",
        "inputs:",
        "  - { key: fact:item, label: \"Item\", type: item, kind: item }",
        "  - { key: fact:issue, label: \"Issue\", type: text, kind: text }",
        "initial: write",
        "states:",
        "  write:",
        "    kind: active",
        "    step: step.report.write",
        "    budget: { tokens: 1000 }",
        "    timeout: { after: 1m, then: park }",
        "    next: \"Write up the resolution for {item}.\"",
        "  run: { kind: wait, timeout: { after: 2d, then: park },",
        "         waitFor: { child: { recipe: workflow.t-child, inputs: { \"fact:request\": { fact: fact:issue } }, onParked: propagate } },",
        "         next: \"Waiting for {child}.\" }",
        "  done: { kind: terminal, outcome: resolved }",
        "transitions:",
        "  - { from: write, to: run, on: step.completed, when: { outcome: done }, produces: [fact:resolution] }",
        "  - { from: run, to: done, on: wait.satisfied, when: { outcome: resolved } }");

    /// <summary>A running dev server (or the time-skipping one), the composed host with the fakes and a worker for the test body.</summary>
    internal sealed class AgentRig : IAsyncDisposable
    {
        private readonly AgentRuntimeTestBase _test;
        private readonly bool _owned;
        private InstanceStart? _start;

        private AgentRig(AgentRuntimeTestBase test, WorkflowEnvironment environment, bool owned, ServiceProvider sindri)
        {
            _test = test;
            Environment = environment;
            _owned = owned;
            Sindri = sindri;
        }

        public WorkflowEnvironment Environment { get; }

        public ServiceProvider Sindri { get; }

        public LaunchId Launch => AgentLaunches.LaunchIdFor(Run());

        public string Worktree => AgentLaunches.WorktreeFor(Run(), new AgentStepTarget(ItemId.From(_item.Id), AgentId.From("claude-code"), Repo));

        public static async Task<AgentRig> Start(AgentRuntimeTestBase test, WorkflowEnvironment? given, CancellationToken ct)
        {
            test._harness.Replacing = services => Register(services, test);
            test._harness.Recipe(RecipeName, _recipe);
            foreach (var yaml in test._also)
            {
                _ = test._harness.Approve(yaml);
            }

            var environment = given ?? await WorkflowsHarness.StartDevServer(ct: ct);
            var sindri = test._harness.Sindri(test._harness.Replacing);
            await WorkflowsHarness.HoldLease(sindri, ct);
            await ApproveTarget(sindri, ct);
            var rig = new AgentRig(test, environment, owned: given is null, sindri);
            rig._start = await rig.Prepare(ct);
            test._diff.Add(rig.Worktree);
            return rig;
        }

        public TemporalWorker Worker() => new(Environment.Client, Sindri.GetRequiredService<TemporalWorkerOptions>());

        public async Task<WorkflowHandle<RecipeWorkflow, RecipeResult>> Start() => await WorkflowsHarness.Start(Environment.Client, _start!);   // why: Start(rig) set it

        public async Task Run(Func<WorkflowHandle<RecipeWorkflow, RecipeResult>, Task> body)
        {
            var ct = TestContext.Current.CancellationToken;
            using var worker = Worker();
            await worker.ExecuteAsync(async () =>
            {
                var handle = await Start();
                await body(handle);
                await Replayed(handle);
                if ((await handle.DescribeAsync()).Status == Temporalio.Api.Enums.V1.WorkflowExecutionStatus.Running)
                {
                    try
                    {
                        await handle.TerminateAsync("test over");
                    }
                    catch (Temporalio.Exceptions.RpcException)
                    {
                        // the instance ended between the describe and the terminate: nothing is left to stop
                    }
                }
            }, ct);
        }

        public static async Task Replayed(WorkflowHandle<RecipeWorkflow, RecipeResult> handle)
        {
            var replayed = await new WorkflowReplayer(new WorkflowReplayerOptions().AddWorkflow<RecipeWorkflow>()).ReplayWorkflowAsync(await handle.FetchHistoryAsync(), throwOnReplayFailure: false, TestContext.Current.CancellationToken);
            Assert.Null(replayed.ReplayFailure);
        }

        /// <summary>The session id of each 'launch.started' row, in order (null when the row has none).</summary>
        public async Task<List<string?>> StartedSessions(CancellationToken ct)
        {
            var rows = (await Sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 200, ct)).Value;
            var sessions = new List<string?>();
            foreach (var row in rows.Where(static e => e.Envelope.Type == "launch.started"))
            {
                using var payload = JsonDocument.Parse(row.Envelope.Payload);
                sessions.Add(payload.RootElement.TryGetProperty("sessionId", out var id) ? id.GetString() : null);
            }

            return sessions;
        }

        /// <summary>The reason of the instance's one 'step.failed' row.</summary>
        public async Task<string?> FailureReason(CancellationToken ct) => (await Failure(ct)).Reason;

        /// <summary>
        /// The reason and the runtime ('via') of the instance's one 'step.failed' row. The park shows on the instance before the mirror activity has written
        /// the row, so this waits for the row; nulls only when none arrives in 30 s.
        /// </summary>
        public async Task<(string? Reason, string? Via)> Failure(CancellationToken ct)
        {
            var failure = (Reason: (string?)null, Via: (string?)null);
            var deadline = RealTime.NowDateTime.AddSeconds(30);
            while (failure.Reason is null && RealTime.NowDateTime < deadline)
            {
                failure = await ReadFailure(ct);
                if (failure.Reason is null)
                {
                    await Task.Delay(50, ct);
                }
            }

            return failure;
        }

        private async Task<(string? Reason, string? Via)> ReadFailure(CancellationToken ct)
        {
            var rows = (await Sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 200, ct)).Value;
            foreach (var row in rows.Where(static e => e.Envelope.Type == "workflow.transition"))
            {
                using var payload = JsonDocument.Parse(row.Envelope.Payload);
                if (payload.RootElement.TryGetProperty("trigger", out var trigger) && trigger.GetString() == "step.failed")
                {
                    var detail = payload.RootElement.GetProperty("detail");
                    return (detail.GetProperty("reason").GetString(), detail.TryGetProperty("via", out var via) ? via.GetString() : null);
                }
            }

            return (null, null);
        }

        public async ValueTask DisposeAsync()
        {
            await Sindri.DisposeAsync();
            if (_owned)
            {
                await Environment.ShutdownAsync();
            }
        }

        private AgentStepRun Run()
        {
            var address = _start!.WorkflowId();   // why: Start(rig) set it before the first use
            return new AgentStepRun(WorkflowInstanceId.Of(address), address, RunToken.For(1, 1), new StepRef("write", "step.report.write"), WriteScope: false);
        }

        private async Task<InstanceStart> Prepare(CancellationToken ct)
        {
            var start = await WorkflowsHarness.RecipeStart(Sindri, _test._root, _item.Key, ct);
            var item = _test._harness.Fact(JsonSerializer.Serialize(new { id = _item.Id, repo = _test._factRepo ?? _item.Repo, spec = (string?)null }));
            var inputs = new Dictionary<FactKey, InputFact>
            {
                [FactKey.From("fact:item")] = new InputFact("item.json", item),
                [FactKey.From("fact:issue")] = new InputFact("issue.txt", _test._harness.Fact("The button is broken")),
            };
            foreach (var (key, text) in _test._more)
            {
                inputs[FactKey.From(key)] = new InputFact(key[5..] + ".txt", _test._harness.Fact(text));
            }

            return start with { Inputs = inputs };
        }

        private static async Task ApproveTarget(IServiceProvider sindri, CancellationToken ct)
        {
            var declared = new SindriProfile(SindriProfile.CurrentSchemaVersion, "tester", Repos: [new RepoEntry(_item.Repo, Repo)],
                Workflow: new WorkflowSettings(Agent: new WorkflowAgentSettings(AgentId.From("claude-code"))));
            var profile = ProfileRules.Check(declared).Value;
            var saved = await sindri.GetRequiredService<IApprovals>().Save(new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml"), ct);
            Assert.False(saved.IsError, saved.IsError ? saved.Error.Message : null);
        }

        private static void Register(IServiceCollection services, AgentRuntimeTestBase test)
        {
            _ = services.Replace(ServiceDescriptor.Singleton(test._launcherSeen ?? test._launcher));
            _ = services.Replace(ServiceDescriptor.Singleton<IAgentSessions>(test._sessions));
            _ = services.Replace(ServiceDescriptor.Singleton(test._diffSeen ?? test._diff));
            _ = services.Replace(ServiceDescriptor.Singleton<IStepOutputs>(test._outputs));
            _ = services.Replace(ServiceDescriptor.Singleton(new AgentRuntimeOptions(PollInterval: TimeSpan.FromMilliseconds(100))));
        }
    }

    internal static readonly string _writeRecipe = string.Join('\n',
        "schema: 1",
        "id: workflow.dev-agent-write",
        "version: 1",
        "inputs:",
        "  - { key: fact:item, label: \"Item\", type: item, kind: item }",
        "  - { key: fact:change, label: \"Change\", type: text, kind: text }",
        "  - { key: fact:review, label: \"Review\", type: text, kind: text }",
        "initial: fix",
        "states:",
        "  fix:",
        "    kind: active",
        "    step: step.review.fix",
        "    budget: { tokens: 1000 }",
        "    timeout: { after: 1m, then: park }",
        "    next: \"Fix the review findings for {item}.\"",
        "  done: { kind: terminal, outcome: resolved }",
        "transitions:",
        "  - { from: fix, to: done, on: step.completed, when: { outcome: fixed }, produces: [fact:change] }");
}
