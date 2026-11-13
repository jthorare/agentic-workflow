using Aw.Context.Testing.Engine;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.ContextCalls;
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

    private RunDoctorService Service() =>
        new(_h.Store, _h.Probe, new LedgerFacts(_h.Store, _views), new ProfileFacts(_h.Profiles,
            new ContextEngineChecks(FakeContextEngine.Instance, new MapPublishDoctorCheck(new MapPublishStatusReader(FakeContextEngine.Instance, new NoIndexOutlooks(), _h.Store, _h.Clock), _h.Clock), new LinearDoctor(FakeContextEngine.Instance, new PointerSecrets(_ => null, new FakeToolRunner(ToolRun.Missing))), new Features.CodeIndexJobs.CodeIndexDoctor(new CodeIndex.Testing.FakeCodeIndex(), new CodeIndex.Testing.FakeRepoHeads(), new InMemoryJobViews(), new Features.CodeIndexJobs.CodeIndexProbes(new CodeIndex.Testing.FakeEmbedder(), new CodeIndex.Testing.FakePins()), _h.Clock, _h.Profiles, new Server.NoPauses()))), new RuntimeFacts(_api, _temporal), _h.Clock);

    [Fact, Trait("Lane", "unit")]
    public async Task RunDoctor_is_green_when_running_approved_and_answering()
    {
        var approved = SliceHarness.Profile();
        _h.Profiles.Live = approved;
        var grant = await _h.HoldLease();
        _api.Answer = (new ControlEndpoint(new Uri("http://127.0.0.1:7410"), grant.Holder.Pid, grant.Epoch), new Ping(grant.Holder.Pid, grant.Epoch));
        _ = await _temporal.Ensure(Token);
        _ = await _views.Open(ViewSchema.Version, Token);

        var report = (await Service().Handle(new RunDoctorRequest(), Token)).Value;

        Assert.Equal("lease,ledger,views,profile,api,temporal,context:map,context:pages,context:roles,context:retention,context:store,context:publish,context:linear,code-index", string.Join(',', report.Checks.Select(c => c.Name)));
        Assert.All(report.Checks.Where(c => c.Name is not ("context:map" or "context:retention")), c => Assert.Equal(CheckState.Ok, c.State));
        Assert.Equal((CheckState.Warn, "no map yet", "sindri context map import <file>"), (report.Checks[6].State, report.Checks[6].Detail, report.Checks[6].Fix));
        Assert.Equal(1, report.ExitCode);   // the test engine has no map, so context:map warns (S-3a); everything else is green
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
    public async Task RunDoctor_fails_the_lease_when_its_holder_died_and_the_edited_profile_is_fine()
    {
        var dead = FakeProcessProbe.Holder(300);
        _h.Probe.Start(dead);
        _ = await _h.Store.Acquire(dead, Token);
        _h.Probe.Kill(300);
        _h.Profiles.Live = SliceHarness.Profile("7s");

        var report = (await Service().Handle(new RunDoctorRequest(), Token)).Value;

        Assert.Equal(CheckState.Fail, report.Checks.Single(c => c.Name == "lease").State);
        var profile = report.Checks.Single(c => c.Name == "profile");
        Assert.Equal(CheckState.Ok, profile.State);   // an edit is live: nothing to approve
    }

    [Fact, Trait("Lane", "unit")]
    public void A_ledger_written_by_a_newer_build_fails_the_ledger_check()
    {
        var check = DoctorRules.Ledger(new SchemaStatus(Applied: 4, Expected: 3));

        Assert.Equal((CheckState.Fail, "ledger"), (check.State, check.Name));
        Assert.Contains("schema 4/3: written by another build", check.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task RunDoctor_names_the_code_and_the_file_when_the_live_profile_is_invalid()
    {
        _h.Profiles.Live = Core.SindriErrors.ProfileInvalid.With("/home/thor/profile.yaml: line 3: unknown key 'tennant'");

        var profile = (await Service().Handle(new RunDoctorRequest(), Token)).Value.Checks.Single(c => c.Name == "profile");

        Assert.Equal(CheckState.Fail, profile.State);
        Assert.Contains("SND-PROFILE-002", profile.Detail, StringComparison.Ordinal);
        Assert.Contains("/home/thor/profile.yaml", profile.Detail, StringComparison.Ordinal);
    }
}
