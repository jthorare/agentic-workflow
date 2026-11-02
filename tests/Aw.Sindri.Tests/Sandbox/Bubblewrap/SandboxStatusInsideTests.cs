using System.Diagnostics;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Testing.Sandbox;
using Aw.Sindri.Tests.Launch;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>
/// SB-5a proof, the net-forward half end to end (sandbox spec §12 SB-5a): a started 'sindri serve' in a scratch AW_SINDRI_HOME, a real bubblewrap sandbox with the
/// launch's egress forwarders and session home, and 'sindri status' run inside it on a private tmux server. It must answer through the session home's control.json
/// and the inside forwarder, with no network but loopback. Heavy lane: it starts serve (Temporal) and needs bwrap, socat and the installed C# binaries.
/// </summary>
[Lane(TestLane.Heavy)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class SandboxStatusInsideTests
{
    [Fact]
    public async Task Sindri_status_inside_the_sandbox_answers_through_the_session_homes_control_json()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "the bubblewrap sandbox is Linux-only");
        Assert.SkipUnless(BubblewrapBox.Unavailable is null, BubblewrapBox.Unavailable ?? string.Empty);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        using var box = SandboxBox.Create(BubblewrapBox.FixtureParent, home);
        var sindri = Path.Combine(box.Host().CsBinDir, "sindri");
        Assert.SkipUnless(File.Exists(sindri), "no installed C# build: run scripts/install-cs.sh --link (the sandbox binds bin/cs/current)");
        using var tmux = new PrivateTmux();
        var ct = TestContext.Current.CancellationToken;

        LinkTemporal(box);
        using var serve = StartServe(sindri, box);
        try
        {
            var endpoint = await WaitForControl(box.SindriHome + "/control.json", serve, ct);
            var request = box.ReadOnly() with { Net = new SandboxNetFacts(box.NetDir, endpoint.Url.Port, 41234) };
            var sandbox = BubblewrapBox.Create();
            var plan = sandbox.Plan(request);
            Assert.True(plan.IsSuccess, plan.IsError ? plan.Error.ToString() : null);

            _ = SessionHome.Create(box.SindriHome, request.Launch, "secret", endpoint);
            await using var egress = StartEgress(plan.Value);
            var wrapped = sandbox.Wrap(Unlaunched(plan.Value), [sindri, "status", "--json"]);
            Assert.True(wrapped.IsSuccess, wrapped.IsError ? wrapped.Error.ToString() : null);

            var output = Path.Combine(box.Root, "status.out");
            var code = Path.Combine(box.Root, "status.code");
            var line = string.Join(' ', wrapped.Value.Select(Quote)) + $" > {Quote(output)} 2>&1; echo $? > {Quote(code)}";
            Assert.NotNull(tmux.Run("new-session", "-d", "-s", "status-inside", "-c", box.Root, "sh", "-c", line));
            await WaitForFile(code, ct);

            await AssertAnswered(output, code, ct);
        }
        finally
        {
            RemoveSessionHome(box);
            Stop(serve);
        }

        Assert.Empty(ProcessesUnder(box.Root));   // serve's temporal dev server too: it is setsid-detached, so only serve's own shutdown stops it
    }

    // Exit 1 is attention (the scratch profile isn't approved); "running":true means serve answered through the forwarder, not SND-API-001 "not running".
    private static async Task AssertAnswered(string output, string code, CancellationToken ct)
    {
        var printed = await File.ReadAllTextAsync(output, ct);
        var exit = (await File.ReadAllTextAsync(code, ct)).Trim();
        Assert.True(string.Equals(exit, "0", StringComparison.Ordinal) || string.Equals(exit, "1", StringComparison.Ordinal), "sindri status inside the sandbox: " + printed);
        Assert.Contains("\"running\":true", printed, StringComparison.Ordinal);
        Assert.Contains("apiVersion", printed, StringComparison.Ordinal);   // `sindri status --json` carries it (aot-smoke expected.txt)
    }

    // This serve never opened the launch, so the CLI's X-Sindri-Launch header would be SND-AUTH-003: the proof is the path to serve, not launch auth.
    private static SandboxPlan Unlaunched(SandboxPlan plan) => plan with { Environment = [.. plan.Environment.Where(static e => e.Name != "AW_SINDRI_LAUNCH")] };

    private static void RemoveSessionHome(SandboxBox box) => SessionHome.Remove(SessionHomePaths.For(box.SindriHome, box.WriteScope().Launch));

    private static RunningEgress StartEgress(SandboxPlan plan)
    {
        var network = new SystemEgressNetwork();
        var started = new EgressService(network, network, Environment.GetEnvironmentVariable, Path.Combine(plan.RunDirectory, "egress")).Start(plan.Network, EgressCaps.Default);
        Assert.True(started.IsSuccess, started.IsError ? started.Error.ToString() : null);
        return started.Value;
    }

    // The box's state dir is a scratch one, so serve would look for the pinned temporal CLI there: point it at the box's real copy.
    private static string PinnedTemporal()
    {
        var state = Environment.GetEnvironmentVariable("AW_STATE_DIR") is { Length: > 0 } dir
            ? dir
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentic-workflow");
        var cache = Path.Combine(state, "cache", "bin");
        return Directory.Exists(cache)
            ? Directory.GetDirectories(cache, "temporal-*").FirstOrDefault(d => !Path.GetFileName(d).StartsWith("temporal-test-server", StringComparison.Ordinal)) ?? Path.Combine(cache, "temporal-missing")
            : Path.Combine(cache, "temporal-missing");
    }

    private static void LinkTemporal(SandboxBox box)
    {
        var temporal = PinnedTemporal();
        Assert.SkipUnless(Directory.Exists(temporal), "the pinned temporal CLI isn't installed: scripts/binaries-lock.sh fetch temporal");
        var cache = Path.Combine(box.State, "cache", "bin");
        _ = Directory.CreateDirectory(cache);
        _ = Directory.CreateSymbolicLink(Path.Combine(cache, Path.GetFileName(temporal)), temporal);
    }

    private static Process StartServe(string sindri, SandboxBox box)
    {
        var info = new ProcessStartInfo(sindri, ["serve"]) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        _ = info.Environment.Remove("TMUX");
        info.Environment["AW_SINDRI_HOME"] = box.SindriHome;
        info.Environment["AW_STATE_DIR"] = box.State;
        info.Environment["AW_SINDRI_PORT"] = "0";
        return Process.Start(info) ?? throw new InvalidOperationException("sindri serve did not start");
    }

    private static async Task<ControlEndpoint> WaitForControl(string path, Process serve, CancellationToken ct)
    {
        for (var waited = 0; waited < 120; waited++)
        {
            if (serve.HasExited)
            {
                Assert.Fail("sindri serve exited early with code " + serve.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture) + ": " + await serve.StandardError.ReadToEndAsync(ct));   // ExitCode throws while it still runs
            }

            if (File.Exists(path) && AwJson.Deserialize<ControlEndpoint>(await File.ReadAllBytesAsync(path, ct), SandboxFilesJson.Options) is { IsSuccess: true } read)
            {
                return read.Value;
            }

            await Task.Delay(500, ct);
        }

        throw new TimeoutException("sindri serve wrote no control.json in 60 s");
    }

    private static async Task WaitForFile(string path, CancellationToken ct)
    {
        for (var waited = 0; waited < 120 && !File.Exists(path); waited++)
        {
            await Task.Delay(500, ct);
        }

        Assert.True(File.Exists(path), "sindri status did not finish inside the sandbox in 60 s");
    }

    private static void Stop(Process serve)
    {
        if (serve.HasExited)
        {
            return;
        }

        // TERM, not Kill: serve's own shutdown stops its temporal dev server (setsid-detached, so a killed serve orphans it), TERM then KILL after 10 s.
        using (var term = Process.Start("kill", ["-TERM", serve.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)]))
        {
            _ = term?.WaitForExit(5_000);
        }

        if (!serve.WaitForExit(30_000))
        {
            serve.Kill(entireProcessTree: false);   // this serve only, by its own handle
            _ = serve.WaitForExit(10_000);
        }
    }

    private static List<string> ProcessesUnder(string root) =>
        [.. Directory.GetDirectories("/proc").Where(static d => int.TryParse(Path.GetFileName(d), out _)).Select(CommandLineOf).Where(c => c.Contains(root, StringComparison.Ordinal))];

    private static string CommandLineOf(string procDir)
    {
        try
        {
            return File.ReadAllText(procDir + "/cmdline").Replace('\0', ' ');
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return string.Empty;   // the process ended while we looked
        }
    }

    private static string Quote(string value) => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
}
