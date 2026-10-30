using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Testing.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>A command in a real bubblewrap box can't find the human token a fake Sindri home holds, by the real path or through AW_SINDRI_HOME (#1099).</summary>
[Lane(TestLane.Integration)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class BubblewrapHumanTokenCanaryTests
{
    private const string TokenText = "human-token-canary-7f3a9c";

    [Fact]
    public async Task A_command_inside_the_box_can_not_read_the_real_homes_human_token()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "the bubblewrap sandbox is Linux-only");
        Assert.SkipUnless(BubblewrapBox.Unavailable is null, BubblewrapBox.Unavailable ?? string.Empty);
        var home = Path.Combine(BubblewrapBox.FixtureParent, "home-" + RealTime.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(home);
        try
        {
            using var box = SandboxBox.Create(BubblewrapBox.FixtureParent, home);
            Directory.CreateDirectory(box.SindriHome + "/auth");
            await File.WriteAllTextAsync(box.SindriHome + "/auth/human.token", TokenText, TestContext.Current.CancellationToken);
            var sandbox = BubblewrapBox.Create();
            var plan = sandbox.Plan(box.WriteScope());
            Assert.True(plan.IsSuccess, plan.IsError ? plan.Error.ToString() : null);
            var script = $"cat '{box.SindriHome}/auth/human.token'; echo \"real=$?\"; cat \"$AW_SINDRI_HOME/auth/human.token\"; echo \"session=$?\"";
            var wrapped = sandbox.Wrap(plan.Value, ["/bin/sh", "-c", script]);
            Assert.True(wrapped.IsSuccess, wrapped.IsError ? wrapped.Error.ToString() : null);

            var ran = new SandboxProcesses().Run(wrapped.Value[0], [.. wrapped.Value.Skip(1)], TimeSpan.FromSeconds(60));

            var all = ran.Output + ran.Error;
            Assert.True(ran.Started && !ran.TimedOut, all);
            Assert.Matches("real=[1-9]", all);
            Assert.Matches("session=[1-9]", all);
            Assert.DoesNotContain(TokenText, all, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }
}
