using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.Launching;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Launch;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.Launching;

/// <summary>The doctor's launcher line: what "run `sindri doctor`" in the launch copy leads to (UI spec E-1): the probe's age, the requests past their deadline and the last failure code.</summary>
[Lane(TestLane.Unit)]
public sealed class LauncherDoctorChecksTests
{
    private readonly ViewsHarness _v = new();
    private readonly FixedLaunchSpend _spend = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<string> Line()
    {
        var report = new LaunchShowReport("Print", "detected", "/home/demo/launcher.json", [], []);
        var setup = new ScriptedLaunchSetup(Results.Success(new LaunchInitReport("print", "detected", Path: null, Json: "{}")), Results.Success(report));
        var reader = new LaunchTargetsReader(_v.Slice.Store, _v.Slice.Events, _v.Slice.Profiles, _spend, _v.Progress);
        var checks = new LauncherDoctorChecks(setup, new LaunchViews(_v.Views), reader, _v.Slice.Clock);
        var rows = await checks.Run(Token);
        return rows.Single(static r => string.Equals(r.Name, "launcher", StringComparison.Ordinal)).Detail;
    }

    [Fact]
    public async Task Before_any_probe_the_line_says_so_and_names_no_failure()
    {
        _ = await _v.Slice.HoldLease();

        var line = await Line();

        Assert.Contains("not probed yet; 0 request(s) past their deadline; last failure none", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_line_carries_the_probe_age_the_requests_past_their_deadline_and_the_last_failure()
    {
        var grant = await _v.Slice.HoldLease();
        await _v.Item(repo: null);
        var probed = new LaunchProbed(
            new TerminalFacts("print", "Print", Present: true, "detected", ConfigPath: null, Problem: null, Detail: null),
            new HostFacts(HostKind.Tmux, Present: true, "3.4", Problem: null), [], []);
        _ = await _v.Slice.Store.Append(_v.Slice.Events.Entry(EventTypes.LaunchProbed, probed, "launch.probed:0"), grant, Token);
        await _v.Request(A.FirstLaunchId);   // no outcome, and about to be 31 s old
        var failed = LaunchId.From("LCH-" + new string('0', 31) + "7");
        await _v.Request(failed);
        await _v.Fail("SND-LAUNCH-009", launch: failed);
        _v.Slice.Clock.Advance(TimeSpan.FromMinutes(3));

        var line = await Line();

        Assert.Contains("probed 3 min ago", line, StringComparison.Ordinal);
        Assert.Contains("1 request(s) past their deadline", line, StringComparison.Ordinal);
        Assert.Contains("last failure SND-LAUNCH-009", line, StringComparison.Ordinal);
    }
}
