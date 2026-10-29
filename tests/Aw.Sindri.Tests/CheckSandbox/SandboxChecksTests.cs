using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.CheckSandbox;
using Aw.Sindri.Testing.Sandbox;

namespace Aw.Sindri.Tests.CheckSandbox;

/// <summary>Check again (sandbox spec §6, §8): every blocker on one report, and the Steps it would resume: oldest first, no more than the free slots.</summary>
[Lane(TestLane.Unit)]
public sealed class SandboxChecksTests
{
    private static readonly DateTimeOffset _noon = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static WorkflowInstanceId Instance(int n) => WorkflowInstanceId.Of(InstanceAddress.From(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"ITM-0000000000000000000000000000000{n}/spec/1")));

    private static SandboxBlocker Blocker(ErrorDefinition error, string reason, ModelLoginSource? source = null) =>
        SandboxBlocker.Of(SandboxRefusal.For(error, reason, source: source));

    private static SandboxChecks Checks(ISandbox sandbox, SandboxBlocker[]? forms = null, SandboxBlocker[]? logins = null, SandboxRequest? fixture = null, ParkedOnSandbox[]? parked = null, int free = 3, SandboxRequest? writeScope = null, bool withLogin = false) =>
        new(sandbox, new Blockers(forms ?? []), new Blockers(logins ?? []), new Fixture(fixture, writeScope), withLogin ? new WithFixtureLogin() : new NoFixtureLogin(), new SandboxResumeList(new Parks(parked ?? []), new Slots(free)));

    [Fact]
    public async Task A_box_with_no_mechanism_and_a_bad_form_and_a_missing_login_reports_all_three_at_once()
    {
        var form = Blocker(SindriErrors.LaunchUnconfinable, SandboxReasons.FormNone);
        var login = Blocker(SindriErrors.LaunchLoginUnavailable, SandboxReasons.NoLogin, ModelLoginSource.SeparateLogin);

        var report = await Checks(new UnavailableSandbox(), [form], [login]).Run(Token);

        Assert.False(report.Ready);
        Assert.Equal(["SND-LAUNCH-014", "SND-LAUNCH-015", "SND-LAUNCH-017"], [.. report.Blockers.Select(b => b.Code.Error.Code)]);
        Assert.Equal(SandboxReasons.UnsupportedPlatform, report.Blockers[0].Reason);
        Assert.Empty(report.ReadyToResume);
    }

    [Fact]
    public async Task A_fixture_plan_that_fails_its_self_check_is_a_016_blocker_and_hides_no_other()
    {
        var login = Blocker(SindriErrors.LaunchLoginUnavailable, SandboxReasons.Expired, ModelLoginSource.OwnerCopy);
        var sandbox = new FailingSelfCheck(SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.FsDeny));

        var report = await Checks(sandbox, logins: [login], fixture: SandboxRequests.ReadOnly).Run(Token);

        Assert.Equal(["SND-LAUNCH-017", "SND-LAUNCH-016"], [.. report.Blockers.Select(b => b.Code.Error.Code)]);
        Assert.Equal(SandboxReasons.FsDeny, report.Blockers[1].Reason);
    }

    [Fact]
    public async Task A_write_scope_fixture_that_fails_its_self_check_is_a_016_blocker()
    {
        var sandbox = new FailingSelfCheck(SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.GitRef));

        var report = await Checks(sandbox, writeScope: SandboxRequests.WriteScope).Run(Token);

        Assert.Equal(SandboxReasons.GitRef, Assert.Single(report.Blockers).Reason);
    }

    [Fact]
    public async Task The_resume_list_keeps_what_the_parks_know_about_each_instance()
    {
        var older = new ParkedOnSandbox(Instance(1), _noon.AddHours(-3), "ITM-1", "Older step");
        var newer = new ParkedOnSandbox(Instance(2), _noon.AddHours(-1), "ITM-2", "Newer step");

        var report = await Checks(new PassThroughSandbox(), parked: [newer, older], free: 1).Run(Token);

        Assert.Equal([Instance(1)], report.ReadyToResume);
        Assert.Equal([older], report.Resume);
    }

    [Fact]
    public async Task No_fixture_skips_the_self_check_instead_of_failing_it()
    {
        var report = await Checks(new PassThroughSandbox(), fixture: null).Run(Token);

        Assert.True(report.Ready);
    }

    [Fact]
    public async Task A_fixture_plan_with_no_login_says_the_hooks_were_not_checked_and_stays_ready()
    {
        var report = await Checks(new PassThroughSandbox(), fixture: SandboxRequests.ReadOnly).Run(Token);

        Assert.True(report.Ready);
        Assert.Equal([SandboxCheckReport.HooksNotChecked], report.NotChecked);
    }

    [Fact]
    public async Task The_hooks_note_is_said_once_when_both_fixtures_plan_with_no_login()
    {
        var report = await Checks(new PassThroughSandbox(), fixture: SandboxRequests.ReadOnly, writeScope: SandboxRequests.WriteScope).Run(Token);

        Assert.Equal([SandboxCheckReport.HooksNotChecked], report.NotChecked);
    }

    [Fact]
    public async Task The_hooks_note_stands_beside_a_blocker_and_does_not_change_the_blockers()
    {
        var form = Blocker(SindriErrors.LaunchUnconfinable, SandboxReasons.FormReserved);

        var report = await Checks(new PassThroughSandbox(), [form], fixture: SandboxRequests.ReadOnly).Run(Token);

        Assert.False(report.Ready);
        _ = Assert.Single(report.Blockers);
        Assert.Equal([SandboxCheckReport.HooksNotChecked], report.NotChecked);
    }

    [Fact]
    public async Task A_fixture_plan_with_a_login_checks_the_hooks_and_notes_nothing()
    {
        var report = await Checks(new PassThroughSandbox(), fixture: SandboxRequests.ReadOnly, withLogin: true).Run(Token);

        Assert.Empty(report.NotChecked);
    }

    [Fact]
    public async Task No_fixture_plans_nothing_so_nothing_is_noted()
    {
        var report = await Checks(new PassThroughSandbox(), fixture: null).Run(Token);

        Assert.Empty(report.NotChecked);
    }

    [Fact]
    public async Task The_response_carries_what_the_report_did_not_check()
    {
        var checks = Checks(new PassThroughSandbox(), fixture: SandboxRequests.ReadOnly);

        var response = (await new CheckSandboxService(checks, new LastSandboxCheck()).Handle(new CheckSandboxRequest(), Token)).Value;

        Assert.Equal([SandboxCheckReport.HooksNotChecked], response.NotChecked);
    }

    [Fact]
    public async Task A_passing_fixture_leaves_the_box_ready()
    {
        var report = await Checks(new PassThroughSandbox(), fixture: SandboxRequests.ReadOnly).Run(Token);

        Assert.True(report.Ready);
        Assert.Empty(report.Blockers);
    }

    [Fact]
    public async Task Ready_lists_the_parked_instances_oldest_first_capped_at_the_free_slots()
    {
        var parked = new[]
        {
            new ParkedOnSandbox(Instance(3), _noon.AddHours(-1)),
            new ParkedOnSandbox(Instance(1), _noon.AddHours(-9)),
            new ParkedOnSandbox(Instance(2), _noon.AddHours(-5)),
        };

        var report = await Checks(new PassThroughSandbox(), parked: parked, free: 2).Run(Token);

        Assert.Equal([Instance(1), Instance(2)], [.. report.ReadyToResume]);
    }

    [Fact]
    public async Task No_free_slot_resumes_nothing()
    {
        var report = await Checks(new PassThroughSandbox(), parked: [new ParkedOnSandbox(Instance(1), _noon)], free: 0).Run(Token);

        Assert.True(report.Ready);
        Assert.Empty(report.ReadyToResume);
    }

    [Fact]
    public async Task A_blocker_that_stands_resumes_nothing_even_with_parked_instances()
    {
        var form = Blocker(SindriErrors.LaunchUnconfinable, SandboxReasons.FormReserved);

        var report = await Checks(new PassThroughSandbox(), [form], parked: [new ParkedOnSandbox(Instance(1), _noon)]).Run(Token);

        Assert.Empty(report.ReadyToResume);
    }

    private sealed class NoFixtureLogin : ISandboxFixtureLogin
    {
        public ValueTask<FixtureLogin?> Prepare(LaunchId launch, CancellationToken cancellationToken) => ValueTask.FromResult<FixtureLogin?>(null);

        public void Release(PreparedLogin login)
        {
        }
    }

    private sealed class WithFixtureLogin : ISandboxFixtureLogin
    {
        public ValueTask<FixtureLogin?> Prepare(LaunchId launch, CancellationToken cancellationToken) => ValueTask.FromResult<FixtureLogin?>(
            new FixtureLogin(AgentId.From("claude-code"), Settings: null, new PreparedLogin(ModelLoginSource.SeparateLogin, [], new Dictionary<string, string>(StringComparer.Ordinal), [], new LoginHandle("fixture"))));

        public void Release(PreparedLogin login)
        {
        }
    }

    private sealed class Blockers(SandboxBlocker[] found) : ISandboxFormCheck, ISandboxLoginCheck
    {
        public ValueTask<IReadOnlyList<SandboxBlocker>> Check(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<SandboxBlocker>>(found);
    }

    private sealed class Fixture(SandboxRequest? request, SandboxRequest? writeScope = null) : ISandboxFixture
    {
        public SandboxRequest? Request() => request;

        public SandboxRequest? WriteScopeRequest() => writeScope;
    }

    private sealed class Parks(ParkedOnSandbox[] parked) : ISandboxParks
    {
        public ValueTask<IReadOnlyList<ParkedOnSandbox>> Parked(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<ParkedOnSandbox>>(parked);
    }

    private sealed class Slots(int free) : ISandboxSlots
    {
        public ValueTask<int> Free(CancellationToken cancellationToken) => ValueTask.FromResult(free);
    }

    private sealed class FailingSelfCheck(Error failure) : ISandbox
    {
        private readonly PassThroughSandbox _inner = new();

        public Result<SandboxCapabilities> Probe() => _inner.Probe();

        public Result<SandboxPlan> Plan(SandboxRequest request) => _inner.Plan(request);

        public Task<Result<Unit>> SelfCheck(SandboxPlan plan, CancellationToken ct) => Task.FromResult<Result<Unit>>(failure);

        public Result<IReadOnlyList<string>> Wrap(SandboxPlan plan, IReadOnlyList<string> argv) => _inner.Wrap(plan, argv);

        public void Discard(SandboxPlan plan) => _inner.Discard(plan);
    }
}
