using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Testing.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>
/// Proof for #2866 in the real bubblewrap sandbox, on a private tmux server: the live profile's folder is bound read-only, so a save the owner makes while a session runs
/// (a temp file in the same folder renamed over profile.yaml) shows up inside that session. Heavy lane: it needs bwrap and socat.
/// </summary>
[Lane(TestLane.Heavy)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class SandboxProfileInsideTests
{
    private static SandboxBox NewBox()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "the bubblewrap sandbox is Linux-only");
        Assert.SkipUnless(BubblewrapBox.Unavailable is null, BubblewrapBox.Unavailable ?? string.Empty);
        return SandboxBox.Create(BubblewrapBox.FixtureParent, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }

    [Fact]
    public async Task A_profile_save_while_the_session_runs_is_seen_inside()
    {
        using var box = NewBox();
        var ct = TestContext.Current.CancellationToken;

        // why: the folder is under the box's root but not under its home, state or Sindri home, because the plan skips the bind when the folder is or holds one of those.
        var folder = Path.Combine(box.Root, "profile-home");
        _ = Directory.CreateDirectory(folder);
        var profile = Path.Combine(folder, "profile.yaml");
        await File.WriteAllTextAsync(profile, "version: one", ct);
        var request = box.WriteScope() with { Host = box.Host() with { ProfilePath = profile } };

        var marker = Path.Combine(box.Worktree, "first-read.marker");   // the worktree is writable inside and visible outside
        var script = $"first=$(cat \"$AW_SINDRI_PROFILE\"); echo \"first=$first\"\n"
            + $"echo \"env=$AW_SINDRI_PROFILE\"\n"
            + $"touch {Quote(marker)}\n"
            + "second=$first\n"
            + "for _ in $(seq 1 100); do second=$(cat \"$AW_SINDRI_PROFILE\"); [ \"$second\" != \"$first\" ] && break; sleep 0.1; done\n"
            + "echo \"second=$second\"\n";

        // why: the save is what an owner's editor does: a temp file in the same folder, renamed over the profile. It starts once the session has read the first version.
        var save = Task.Run(
            async () =>
            {
                for (var waited = 0; waited < 600 && !File.Exists(marker); waited++)
                {
                    await Task.Delay(100, ct);
                }

                var temp = profile + ".tmp";
                await File.WriteAllTextAsync(temp, "version: two", ct);
                File.Move(temp, profile, overwrite: true);
            },
            ct);

        var ran = await RunInside(box, script, request);
        await save;

        Assert.True(ran.Exit == 0, "the script failed inside the sandbox: " + ran.Output);
        Assert.Contains("first=version: one", ran.Output, StringComparison.Ordinal);
        Assert.Contains("second=version: two", ran.Output, StringComparison.Ordinal);
        Assert.Contains("env=" + profile, ran.Output, StringComparison.Ordinal);
        Assert.Empty(ProcessListing.Under(box.Root));
    }

    // Plans the box's write-scope request, starts the launch's egress forwarders and runs 'sh -c script' wrapped, on a private tmux server. Output and exit code come back through files.
        private static async Task<Ran> RunInside(SandboxBox box, string script, SandboxRequest request)
    {
        using var tmux = new PrivateTmux();
        var ct = TestContext.Current.CancellationToken;
        var sandbox = BubblewrapBox.Create();
        var plan = sandbox.Plan(request);
        Assert.True(plan.IsSuccess, plan.IsError ? plan.Error.ToString() : null);

        var running = plan.Value;
        await using var egress = StartEgress(running);
        var wrapped = sandbox.Wrap(running, ["/bin/sh", "-c", script]);
        Assert.True(wrapped.IsSuccess, wrapped.IsError ? wrapped.Error.ToString() : null);

        var output = Path.Combine(box.Root, "inside.out");
        var code = Path.Combine(box.Root, "inside.code");
        File.Delete(output);   // a second run in one box must not read the first one's files
        File.Delete(code);
        var line = string.Join(' ', wrapped.Value.Select(Quote)) + $" > {Quote(output)} 2>&1; echo $? > {Quote(code)}";
        var run = Path.Combine(box.Root, "inside.sh");
        await File.WriteAllTextAsync(run, line, ct);   // why: a write-scope argv is longer than tmux's message limit (about 16 KB), so tmux runs it from a file
        Assert.NotNull(tmux.Run("new-session", "-d", "-s", "profile-inside", "-c", box.Root, "sh", run));
        for (var waited = 0; waited < 1200 && !File.Exists(code); waited++)
        {
            await Task.Delay(100, ct);
        }

        Assert.True(File.Exists(code), "the script did not finish inside the sandbox in 120 s");
        return new Ran(int.Parse((await File.ReadAllTextAsync(code, ct)).Trim(), System.Globalization.CultureInfo.InvariantCulture), await File.ReadAllTextAsync(output, ct));
    }

    private static RunningEgress StartEgress(SandboxPlan plan)
    {
        var network = new SystemEgressNetwork();
        var started = new EgressService(network, network, Environment.GetEnvironmentVariable, Path.Combine(plan.RunDirectory, "egress")).Start(plan.Network, EgressCaps.Default);
        Assert.True(started.IsSuccess, started.IsError ? started.Error.ToString() : null);
        return started.Value;
    }

    private static string Quote(string value) => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private sealed record Ran(int Exit, string Output);
}
