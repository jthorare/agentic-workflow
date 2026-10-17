using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.Launching;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.Launching;

/// <summary>
/// The launcher service over in-memory parts: a ledger and the views folded from it (every event the service appends is folded before it looks
/// again), a fake launcher, session host and viewer, and a runtime that takes instances over. A restart is a new <see cref="LaunchWork"/>.
/// </summary>
internal sealed class LauncherRig
{
    private static readonly RepoName _repo = RepoName.From("demo");
    private long _synced;

    public LauncherRig()
    {
        Status = new HookedStatus(H.Progress);
        Port = Launcher;
        Timings = LaunchTimings.For(H.Slice.Clock);
    }

    public ViewsHarness H { get; } = new();

    public SliceHarness Slice => H.Slice;

    public FakeLauncher Launcher { get; } = new();

    public ILauncher Port { get; set; }

    public FakeSessionHost Host { get; } = new();

    public FakeTerminalViewer Viewer { get; } = new();

    public FakeOpenRuntime Runtime { get; } = new();

    public LaunchLiveness Liveness { get; } = new();

    public FixedLaunchSpend Spend { get; } = new();

    public FakeRepoLocator Repos { get; } = new();

    public HookedStatus Status { get; }

    public LaunchWork Work { get; private set; } = new();

    public LaunchTimings Timings { get; set; }

    public LeaseGrant Grant { get; private set; } = null!;

    public IDisposable Caller() => Slice.Callers.EnterService("launcher");

    public async Task Boot()
    {
        Grant = await Slice.HoldLease();
        var profile = ProfileRules.Check(new SindriProfile(1, "thor", Repos: [new RepoEntry(_repo.Value, "/work/demo")])).Value;
        _ = await Slice.Profiles.Save(new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml"), TestContext.Current.CancellationToken);
    }

    /// <summary>Approves the boot profile with <paramref name="agents"/> added: the budgets the launcher service re-checks.</summary>
    /// <param name="agents">The 'agents:' block.</param>
    public async Task Approve(AgentSettings agents)
    {
        var profile = ProfileRules.Check(new SindriProfile(1, "thor", Repos: [new RepoEntry(_repo.Value, "/work/demo")], Agents: agents)).Value;
        _ = await Slice.Profiles.Save(new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml"), TestContext.Current.CancellationToken);
    }

    private LaunchProbeRecorder Probes => new(Port, Slice.Profiles, Repos, Slice.Store, Slice.Events);

    public void Restart() => Work = new LaunchWork();

    private LaunchOutcomes Outcomes => new(Slice.Store, Slice.Events, Viewer, Liveness, Slice.Clock);

    private RunLaunch Run => new(
        new LaunchRecovery(Slice.Store, Status, H.Views, Outcomes, Work, Timings), new LaunchIntake(Slice.Store, Slice.Events, Work), Outcomes, Host, Probes, Work, Timings);

    private ProcessLaunch Process
    {
        get
        {
            var plans = new LaunchPlans(H.Views, Slice.Profiles, Spend);
            return new ProcessLaunch(
                Outcomes, new TakeoverWait(Status, Slice.Store, H.Views, Runtime, Timings), Port,
                new AttachFlow(new LaunchViews(H.Views), Viewer, Port, plans, Outcomes, Timings), plans, Work, Timings);
        }
    }

    public async Task<IReadOnlyList<PendingLaunch>> Tick()
    {
        await Sync();
        var ticked = await Run.Handle(new RunLaunchRequest(Grant), TestContext.Current.CancellationToken);
        Assert.True(ticked.IsSuccess, ticked.IsError ? ticked.Error.ToString() : null);
        await Sync();
        return ticked.Value.Due;
    }

    public async Task<bool> Launch(PendingLaunch pending)
    {
        await Sync();
        var done = await Process.Handle(new ProcessLaunchRequest(Grant, pending), TestContext.Current.CancellationToken);
        Assert.True(done.IsSuccess, done.IsError ? done.Error.ToString() : null);
        await Sync();
        return done.Value.Launched;
    }

    public Task Item() =>
        Append(EventTypes.ItemCreated, new ItemCreated(Slice.Ids.New<CommandId>(), A.FirstItemId, "Ship it", _repo, Link: null, Body: null, Owner: null), "item.created:" + A.FirstItemId.Value, A.FirstItemId);

    public Task Request(LaunchId launch, LaunchMode mode = LaunchMode.Open) =>
        Append(EventTypes.ItemOpenRequested, new ItemOpenRequested(launch, A.FirstItemId, mode, AgentId.From("claude-code"), "click"), "item.open-requested:" + launch.Value, A.FirstItemId);

    public Task Instance(string address) =>
        Append(EventTypes.WorkflowStarted, new WorkflowEventPayload(address, "produce", "produce", "start", Outcome: null, Run: null, Detail: null), "workflow.started:" + address, A.FirstItemId);

    public static string InstanceAddress => A.FirstItemId.Value + "/step/1";

    public IReadOnlyList<LedgerEvent> Recorded(string type) => [.. Slice.Store.Events.Where(e => string.Equals(e.Envelope.Type, type, StringComparison.Ordinal))];

    public T Payload<T>(LedgerEvent recorded) => Slice.Events.Payload<T>(recorded).Value;

    public IReadOnlyList<LaunchFailed> Failures => [.. Recorded(EventTypes.LaunchFailed).Select(Payload<LaunchFailed>)];

    public async Task Append<T>(string type, T payload, string key, ItemId item)
    {
        var appended = await Slice.Store.Append(Slice.Events.Entry(type, payload, key, item.Value), Grant, TestContext.Current.CancellationToken);
        Assert.True(appended.IsSuccess, appended.IsError ? appended.Error.ToString() : null);
        await Sync();
    }

    public async Task Sync()
    {
        var page = (await Slice.Store.ReadAfter(_synced, 1000, TestContext.Current.CancellationToken)).Value;
        if (page.Count > 0)
        {
            await H.Apply(page);
            _synced = page[^1].Sequence;
        }
    }

    public static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 500 && !condition(); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition(), "the condition never held");
    }
}
