using System.Text;
using Aw.Context.Testing.Engine;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.GetStatus;

/// <summary>
/// A control plane that runs an older build than the one 'bin/cs/current' names says so (#1032 ask 2): 'sindri status' prints a "stale build" problem with the fix 'sindri stop &amp;&amp; sindri', its
/// JSON carries the same problem in health, and 'sindri doctor' warns; with the same build there is none of it. Both stamps are the folder names under 'bin/cs/', read through IBuildStamps.
/// </summary>
public sealed class StaleBuildCheckTests
{
    private const string Fix = "sindri stop && sindri";
    private static readonly ControlEndpoint _endpoint = new(new Uri("http://127.0.0.1:7410"), 42, 3);

    private readonly ViewsHarness _v = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private SliceHarness H => _v.Slice;

    private async Task<GetStatusResponse> Status(IBuildStamps stamps)
    {
        var approved = SliceHarness.Profile("5s");
        H.Profiles.Live = approved;
        _ = await H.HoldLease();
        _ = await new ApproveProfileService(H.Profiles, H.Profiles, H.Held, H.Store, H.Events, new ProfileChecks(FakeContextEngine.Instance, new FixedAgentCatalog())).Handle(new ApproveProfileRequest(approved.Hash, H.Confirm(approved.Hash)), Token);
        var service = new GetStatusService(H.Held, new ProfileReads(H.Profiles, H.Profiles), H.Store, H.Events, _v.Progress, new HealthServiceRows(_v.Views, [], builds: stamps), new InMemorySessionSnapshot());
        return (await service.Handle(new GetStatusRequest(), Token)).Value;
    }

    private async Task<IReadOnlyList<DoctorCheck>> Doctor(IBuildStamps stamps)
    {
        var views = new InMemoryViewStore(SliceHarness.Json);
        var service = new RunDoctorService(H.Store, H.Probe, new LedgerFacts(H.Store, views), new ProfileFacts(H.Profiles, H.Profiles, H.Store, H.Events,
            new ContextEngineChecks(FakeContextEngine.Instance, new OnyxDoctor(FakeContextEngine.Instance, new PointerSecrets(_ => null, new FakeToolRunner(ToolRun.Missing))), new MapPublishDoctorCheck(new MapPublishStatusReader(FakeContextEngine.Instance, new NoIndexOutlooks(), H.Store, H.Clock), H.Clock), new LinearDoctor(FakeContextEngine.Instance, new PointerSecrets(_ => null, new FakeToolRunner(ToolRun.Missing))), new Features.CodeIndexJobs.CodeIndexDoctor(new CodeIndex.Testing.FakeCodeIndex(), new CodeIndex.Testing.FakeRepoHeads(), new InMemoryJobViews(), new CodeIndex.Testing.FakeEmbedder(), new CodeIndex.Testing.FakePins(), H.Clock, H.Profiles))),
            new RuntimeFacts(new FakeControlPlaneProbe(), new FakeTemporalServer(), stamps), H.Clock);
        return (await service.Handle(new RunDoctorRequest(), Token)).Value.Checks;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_text_names_a_stale_build_and_the_fix_when_the_running_stamp_is_not_the_installed_one()
    {
        var status = await Status(new Stamps(Running: "old-stamp", Installed: "new-stamp"));

        var text = SummaryText.Render(status, _endpoint, ui: null);

        Assert.Contains("stale build", text, StringComparison.Ordinal);
        Assert.Contains(Fix, text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_json_carries_the_stale_build_problem_in_health()
    {
        var status = await Status(new Stamps(Running: "old-stamp", Installed: "new-stamp"));

        var json = Encoding.UTF8.GetString(AwJson.Serialize(status.Health, SliceHarness.Json));

        Assert.Contains("stale build", json, StringComparison.Ordinal);
        Assert.Contains(Fix, json, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Doctor_warns_about_a_stale_build_with_the_fix()
    {
        var checks = await Doctor(new Stamps(Running: "old-stamp", Installed: "new-stamp"));

        var stale = Assert.Single(checks, c => c.Detail.Contains("stale build", StringComparison.Ordinal));
        Assert.Equal((CheckState.Warn, Fix), (stale.State, stale.Fix));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_same_build_prints_no_stale_line_in_status_or_doctor()
    {
        var same = new Stamps(Running: "new-stamp", Installed: "new-stamp");

        var text = SummaryText.Render(await Status(same), _endpoint, ui: null);
        var checks = await Doctor(same);

        Assert.DoesNotContain("stale build", text, StringComparison.Ordinal);
        Assert.DoesNotContain(checks, c => c.Detail.Contains("stale build", StringComparison.Ordinal));
    }

    private sealed record Stamps(string? Running, string? Installed) : IBuildStamps
    {
        string? IBuildStamps.Running() => Running;

        string? IBuildStamps.Installed() => Installed;
    }
}
