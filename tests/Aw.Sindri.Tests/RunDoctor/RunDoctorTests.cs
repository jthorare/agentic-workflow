using Aw.Context.Testing.Engine;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.RunDoctor;

public sealed class RunDoctorTests
{
    private readonly SliceHarness _h = new();
    private readonly FakeControlPlaneProbe _api = new();
    private readonly FakeTemporalServer _temporal = new();
    private readonly InMemoryViewStore _views = new(SliceHarness.Json);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private RunDoctorService Service(ILedgerStore? ledger = null) =>
        new(_h.Store, _h.Probe, new LedgerFacts(_h.Store, _views), new ProfileFacts(_h.Profiles, _h.Profiles, ledger ?? _h.Store, _h.Events,
            new ContextEngineChecks(FakeContextEngine.Instance, new OnyxDoctor(FakeContextEngine.Instance, new PointerSecrets(_ => null, new FakeToolRunner(ToolRun.Missing))))), _api, _temporal, _h.Clock);

    [Fact, Trait("Lane", "unit")]
    public async Task RunDoctor_is_green_when_running_approved_and_answering()
    {
        var approved = SliceHarness.Profile();
        _h.Profiles.Live = approved;
        var grant = await _h.HoldLease();
        _ = await new ApproveProfileService(_h.Profiles, _h.Profiles, _h.Held, _h.Store, _h.Events, _h.Challenges, FakeContextEngine.Instance).Handle(new ApproveProfileRequest(approved.Hash, _h.Confirm(approved.Hash)), Token);
        _api.Answer = (new ControlEndpoint(new Uri("http://127.0.0.1:7410"), grant.Holder.Pid, grant.Epoch), new Ping(grant.Holder.Pid, grant.Epoch));
        _ = await _temporal.Ensure(Token);
        _ = await _views.Open(ViewSchema.Version, Token);

        var report = (await Service().Handle(new RunDoctorRequest(), Token)).Value;

        Assert.Equal("lease,ledger,views,profile,api,temporal,context:map,context:retention,context:onyx", string.Join(',', report.Checks.Select(c => c.Name)));
        Assert.All(report.Checks.Where(c => c.Name is not ("context:map" or "context:retention")), c => Assert.Equal(CheckState.Ok, c.State));
        Assert.Equal((CheckState.Warn, "no map yet", "sindri context map import <file>"), (report.Checks[6].State, report.Checks[6].Detail, report.Checks[6].Fix));
        Assert.Equal(1, report.ExitCode);   // the test engine has no map, so context:map warns (S-3a); everything else, context:onyx included, is green
    }

    [Fact, Trait("Lane", "unit")]
    public async Task RunDoctor_reports_the_views_line_after_the_ledger_line()
    {
        var before = (await Service().Handle(new RunDoctorRequest(), Token)).Value.Checks.Single(c => c.Name == "views");
        _ = await _views.Open(ViewSchema.Version, Token);
        var after = (await Service().Handle(new RunDoctorRequest(), Token)).Value.Checks.Single(c => c.Name == "views");

        Assert.Equal((CheckState.Warn, "no views yet"), (before.State, before.Detail));
        Assert.Equal((CheckState.Ok, "cursor 0 of head 0"), (after.State, after.Detail));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task RunDoctor_fails_with_a_fix_when_nothing_runs_and_nothing_is_approved()
    {
        var report = (await Service().Handle(new RunDoctorRequest(), Token)).Value;

        Assert.Equal(2, report.ExitCode);
        var api = report.Checks.Single(c => c.Name == "api");
        Assert.Equal((CheckState.Fail, "sindri"), (api.State, api.Fix));
        var temporal = report.Checks.Single(c => c.Name == "temporal");
        Assert.Equal((CheckState.Fail, "sindri"), (temporal.State, temporal.Fix));
        Assert.Equal(CheckState.Fail, report.Checks.Single(c => c.Name == "profile").State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task RunDoctor_fails_the_lease_when_its_holder_died_and_warns_on_a_changed_profile()
    {
        var dead = FakeProcessProbe.Holder(300);
        _h.Probe.Start(dead);
        _ = await _h.Store.Acquire(dead, Token);
        _h.Probe.Kill(300);
        _ = await _h.Profiles.Save(SliceHarness.Profile("5s"), Token);
        _h.Profiles.Live = SliceHarness.Profile("7s");

        var report = (await Service().Handle(new RunDoctorRequest(), Token)).Value;

        Assert.Equal(CheckState.Fail, report.Checks.Single(c => c.Name == "lease").State);
        var profile = report.Checks.Single(c => c.Name == "profile");
        Assert.Equal(CheckState.Warn, profile.State);
        Assert.Equal("sindri profile approve", profile.Fix);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task RunDoctor_warns_when_the_saved_approval_isnt_recorded_in_the_ledger()
    {
        var live = SliceHarness.Profile("5s");
        _h.Profiles.Live = live;
        _ = await _h.Profiles.Save(live, Token);   // saved, but its event never reached the ledger

        var profile = (await Service().Handle(new RunDoctorRequest(), Token)).Value.Checks.Single(c => c.Name == "profile");

        Assert.Equal((CheckState.Warn, "sindri profile approve"), (profile.State, profile.Fix));
        Assert.Equal($"approval {live.Hash.Abbreviated} is saved but not recorded in the ledger", profile.Detail);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task RunDoctor_fails_the_profile_check_when_the_ledger_cant_be_read()
    {
        var live = SliceHarness.Profile("5s");
        _h.Profiles.Live = live;
        _ = await _h.Profiles.Save(live, Token);

        var profile = (await Service(new ReadFailingLedger(_h.Store)).Handle(new RunDoctorRequest(), Token)).Value.Checks.Single(c => c.Name == "profile");

        Assert.Equal((CheckState.Fail, ReadFailingLedger.Failure.Message), (profile.State, profile.Detail));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_ledger_written_by_a_newer_build_fails_the_ledger_check()
    {
        var check = DoctorRules.Ledger(new SchemaStatus(Applied: 4, Expected: 3));

        Assert.Equal((CheckState.Fail, "ledger"), (check.State, check.Name));
        Assert.Contains("schema 4/3: written by another build", check.Detail, StringComparison.Ordinal);
    }
}
