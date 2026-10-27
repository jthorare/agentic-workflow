using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Testing.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>The login canary against a real sandbox, on a fixture home so the owner's own agent home is never involved.</summary>
[Lane(TestLane.Integration)]
public sealed class BubblewrapLoginCanaryTests
{
    private static async Task<string?> SelfCheckReason(Func<SandboxPlan, SandboxPlan> change)
    {
        var home = Path.Combine(BubblewrapBox.FixtureParent, "home-" + RealTime.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(home + "/.claude");
        await File.WriteAllTextAsync(home + "/.claude/.credentials.json", "{\"claudeAiOauth\":{\"accessToken\":\"owner-original\"}}", TestContext.Current.CancellationToken);
        using var box = SandboxBox.Create(BubblewrapBox.FixtureParent, home);
        try
        {
            var sandbox = BubblewrapBox.Create();
            var plan = change(sandbox.Plan(box.WriteScope()).Value);

            var result = await sandbox.SelfCheck(plan, TestContext.Current.CancellationToken);

            return result.IsSuccess ? null : SandboxRefusal.ReasonOf(result.Error) + ": " + result.Error.Message;
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public async Task Login_canary_passes_with_the_original_masked_and_the_copy_readable()
    {
        Assert.SkipUnless(BubblewrapBox.Unavailable is null, BubblewrapBox.Unavailable ?? string.Empty);

        var failure = await SelfCheckReason(static plan => plan);

        // SelfCheck stops at the first failure, so "no login failure" proves the login canary ran only if every canary before it passed: the failure, if any, must come from one after it.
        Assert.True(
            failure is null || failure.StartsWith("hooks-run: ", StringComparison.Ordinal) || failure.StartsWith("net-deny: ", StringComparison.Ordinal),
            failure);
    }

    [Fact]
    public async Task Login_canary_fails_when_the_owner_login_is_writable()
    {
        Assert.SkipUnless(BubblewrapBox.Unavailable is null, BubblewrapBox.Unavailable ?? string.Empty);

        var failure = await SelfCheckReason(static plan => plan with
        {
            Mounts = [.. plan.Mounts.Where(static m => !(m.Kind == PlanMountKind.Deny && m.Path.EndsWith("/.claude/.credentials.json", StringComparison.Ordinal)))
                .Select(static m => m.Path.EndsWith("/.claude", StringComparison.Ordinal) ? m with { Kind = PlanMountKind.ReadWrite } : m)],
        });

        Assert.True(failure?.StartsWith("login: ", StringComparison.Ordinal), failure);
        Assert.Contains("original login", failure, StringComparison.Ordinal);
    }
}
