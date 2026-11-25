using System.Net;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Host.Commands.Probe;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Commands.Probe;

/// <summary>
/// The probe itself (names only, the whole ancestry, the version), the doctor's harness lines (warn, never ok, on a
/// mismatch or a pending probe), and a refusal naming the variable or process it found (engine spec §6.2).
/// </summary>
public sealed class HarnessProbeTests : IDisposable
{
    private const string Home = "/Users/owner";
    private const string Refusal = "StopProcess is a human decision. Ask the human to run it in a terminal or the Sindri app: sindri stop";
    private static readonly FakeControlPlaneProbe _api = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static IReadOnlyList<ProcessAncestor> InClaude => FakeProcessAncestry.UnderProcesses(Environment.ProcessId,
        ("zsh", "/bin/zsh"), ("claude", $"{Home}/.local/share/claude/versions/2.1.296"), ("-zsh", "/bin/zsh"), ("stable", "/Applications/Warp.app/Contents/MacOS/stable"))
        .Ancestors(Environment.ProcessId, HarnessProbe.Depth);

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_probe_records_names_the_whole_ancestry_and_the_version_from_the_executable()
    {
        var versions = new FakeHarnessVersions();

        var report = await HarnessProbe.Take(new ProbeInput(Harness: null, ["PATH", "CLAUDECODE", "HOME", "CLAUDE_PID"], Home, InClaude, Terminal: false), versions, Token);

        Assert.Equal(new HarnessProbeReport("claude-code", ["CLAUDECODE", "CLAUDE_PID", "HOME", "PATH"],
            [new("zsh", "/bin/zsh"), new("claude", "~/.local/share/claude/versions/2.1.296"), new("-zsh", "/bin/zsh"),
             new("stable", "/Applications/Warp.app/Contents/MacOS/stable"), new("launchd", "/sbin/launchd")], Terminal: false, "2.1.296"),
            report with { Variables = [.. report.Variables], Ancestry = [.. report.Ancestry] }, new ReportComparer());
        Assert.Empty(versions.Reads);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_named_harness_without_a_versioned_path_asks_its_cli()
    {
        var ancestry = FakeProcessAncestry.UnderProcesses(Environment.ProcessId, ("codex", "/opt/homebrew/bin/codex")).Ancestors(Environment.ProcessId, HarnessProbe.Depth);

        var report = await HarnessProbe.Take(new ProbeInput("codex:workspace-write", ["CODEX_SANDBOX"], Home, ancestry, Terminal: true), new FakeHarnessVersions().With("codex", "0.41.0"), Token);

        Assert.Equal(("codex:workspace-write", "0.41.0", true), (report.Harness, report.Version, report.Terminal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_dev_a_mismatched_pinned_version_warns_with_the_golden_fix()
    {
        var pinned = new[] { Codex(pending: false, "0.40.0") };

        var checks = await HarnessDoctor.Checks(pinned, Live(), new FakeHarnessVersions().With("codex", "0.41.0"), dev: true, Token);

        var check = Assert.Single(checks);
        Assert.Equal(("codex:workspace-write", CheckState.Warn), (check.Name, check.State));
        Assert.Contains("codex 0.41.0 differs from the pinned probe (0.40.0)", check.Detail, StringComparison.Ordinal);
        Assert.Contains("tests/Aw.Sindri.Tests/Golden/Probe/", check.Fix, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_dev_a_pending_owner_probe_warns_and_a_matching_version_is_ok()
    {
        var pinned = new[] { Codex(pending: true, version: null), Codex(pending: false, "0.41.0") with { Harness = "codex:read-only" } };

        var checks = await HarnessDoctor.Checks(pinned, Live(), new FakeHarnessVersions().With("codex", "0.41.0"), dev: true, Token);

        Assert.Equal([("codex:workspace-write", CheckState.Warn, "pending: owner probe"), ("codex:read-only", CheckState.Ok, "codex 0.41.0 matches the pinned probe")],
            checks.Select(c => (c.Name, c.State, c.Detail)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_dev_doctor_re_probes_the_claude_code_it_runs_in()
    {
        var claude = PinnedProbe.All[0];
        var live = await HarnessProbe.Take(new ProbeInput(Harness: null, [.. claude.Markers.Skip(1), "PATH"], Home, InClaude, Terminal: false), new FakeHarnessVersions(), Token);
        var versions = new FakeHarnessVersions();

        var checks = await HarnessDoctor.Checks([claude], live, versions, dev: true, Token);

        var check = Assert.Single(checks);
        Assert.Equal((CheckState.Warn, $"re-probed: {claude.Markers[0]} is no longer set"), (check.State, check.Detail));
        Assert.Empty(versions.Reads);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_user_run_shows_a_differing_installed_version_as_ok_without_a_fix()
    {
        var checks = await HarnessDoctor.Checks([Codex(pending: false, "0.8")], Live(), new FakeHarnessVersions().With("codex", "0.9"), dev: false, Token);

        var check = Assert.Single(checks);
        Assert.Equal((CheckState.Ok, "codex 0.9 installed (probe pinned at 0.8)", null), (check.State, check.Detail, check.Fix));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_user_run_shows_a_pending_owner_probe_as_ok_provisional_markers()
    {
        var checks = await HarnessDoctor.Checks([Codex(pending: true, version: null)], Live(), new FakeHarnessVersions(), dev: false, Token);

        var check = Assert.Single(checks);
        Assert.Equal(CheckState.Ok, check.State);
        Assert.Contains("provisional markers", check.Detail, StringComparison.Ordinal);
        Assert.Null(check.Fix);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_user_run_shows_a_differing_version_with_every_marker_set_as_ok()
    {
        var claude = PinnedProbe.All[0];
        var live = await HarnessProbe.Take(new ProbeInput(Harness: null, [.. claude.Markers, "PATH"], Home, InClaude, Terminal: false), new FakeHarnessVersions(), Token);
        live = live with { Version = "9.9.999" };

        var checks = await HarnessDoctor.Checks([claude], live, new FakeHarnessVersions(), dev: false, Token);

        var check = Assert.Single(checks);
        Assert.Equal(CheckState.Ok, check.State);
        Assert.Equal($"{claude.VersionCommand} 9.9.999 (probe pinned at {claude.Version}; the markers are all set)", check.Detail);
        Assert.Null(check.Fix);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_user_run_keeps_the_missing_marker_warn_but_the_fix_is_not_the_golden_file()
    {
        var claude = PinnedProbe.All[0];
        var live = await HarnessProbe.Take(new ProbeInput(Harness: null, [.. claude.Markers.Skip(1), "PATH"], Home, InClaude, Terminal: false), new FakeHarnessVersions(), Token);

        var checks = await HarnessDoctor.Checks([claude], live, new FakeHarnessVersions(), dev: false, Token);

        var check = Assert.Single(checks);
        Assert.Equal((CheckState.Warn, $"re-probed: {claude.Markers[0]} is no longer set"), (check.State, check.Detail));
        Assert.NotNull(check.Fix);
        Assert.DoesNotContain("Golden", check.Fix, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_dev_a_differing_version_with_every_marker_set_warns_with_the_golden_fix()
    {
        var claude = PinnedProbe.All[0];
        var live = await HarnessProbe.Take(new ProbeInput(Harness: null, [.. claude.Markers, "PATH"], Home, InClaude, Terminal: false), new FakeHarnessVersions(), Token);
        live = live with { Version = "9.9.999" };

        var checks = await HarnessDoctor.Checks([claude], live, new FakeHarnessVersions(), dev: true, Token);

        var check = Assert.Single(checks);
        Assert.Equal(CheckState.Warn, check.State);
        Assert.Contains("tests/Aw.Sindri.Tests/Golden/Probe/", check.Fix, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_refusal_names_why_the_cli_called_as_a_session()
    {
        var session = AgentSession.Detect(_ => null, interactive: true);
        using var client = new ControlPlaneClient(_api, SliceHarness.Json,
            CallerHeaders.For(session, new InMemoryHumanTokenStore(), _ => ValueTask.FromResult<Core.Ids.RepoName?>(null)),
            handler: new CliStubApi(HttpStatusCode.Forbidden, $$"""{"type":"about:blank","title":"SND-AUTH-002","status":403,"detail":"{{Refusal}}"}"""));

        var code = await new Verbs(client, _api, new Output(_out, _error, SliceHarness.Json), new ServiceCollection().BuildServiceProvider()).Stop(Token);

        Assert.Equal((2, $"SND-AUTH-002 {Refusal}\n"), (code, _error.ToString().ReplaceLineEndings("\n")));
    }

    private static HarnessProbeReport Live() => new("unknown", ["PATH"], [new("launchd", "/sbin/launchd")], Terminal: true, Version: null);

    private static PinnedHarness Codex(bool pending, string? version) =>
        new("codex:workspace-write", "codex", Human: false, pending, "codex", version, ["CODEX_SANDBOX"], [new HarnessProcess("codex", ExecutablePart: null)], new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>Records hold lists, so compare them by content.</summary>
    private sealed class ReportComparer : IEqualityComparer<HarnessProbeReport>
    {
        public bool Equals(HarnessProbeReport? x, HarnessProbeReport? y) =>
            x is not null && y is not null && (x.Harness, x.Terminal, x.Version) == (y.Harness, y.Terminal, y.Version)
            && x.Variables.SequenceEqual(y.Variables, StringComparer.Ordinal) && x.Ancestry.SequenceEqual(y.Ancestry);

        public int GetHashCode(HarnessProbeReport obj) => obj.Harness.GetHashCode(StringComparison.Ordinal);
    }
}
