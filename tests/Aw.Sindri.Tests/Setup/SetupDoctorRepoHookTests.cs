using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Setup;

/// <summary>Doctor's per-repo 'pre-commit:&lt;repo&gt;' line (cutover spec §6.2 item 2): it replaces the single 'setup:repo-hook' warning once the hook reader is registered.</summary>
public sealed class SetupDoctorRepoHookTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<IReadOnlyList<DoctorCheck>> All(params RepoHookState[] states) =>
        await new SetupDoctor(InMemorySetupFacts.AllDone(), build: null, new FixedHooks(states)).Run(Token);

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_with_the_v3_hook_is_ok()
    {
        var checks = await All(new RepoHookState("demo", "/work/demo", HookKind.V3));

        var check = Assert.Single(checks, static c => c.Name == "pre-commit:demo");
        Assert.Equal((CheckState.Ok, null), (check.State, check.Fix));
        Assert.DoesNotContain(checks, static c => c.Name == "setup:repo-hook");
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(HookKind.None)]
    [InlineData(HookKind.V2)]
    [InlineData(HookKind.V1)]
    public async Task A_repo_with_no_hook_or_a_ts_hook_warns_with_the_install_command(HookKind kind)
    {
        var check = Assert.Single(await All(new RepoHookState("demo", "/work/demo", kind)), static c => c.Name == "pre-commit:demo");

        Assert.Equal(CheckState.Warn, check.State);
        Assert.Equal("sindri scrub --install-pre-commit --repo /work/demo", check.Fix);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_with_a_foreign_hook_warns_and_says_to_add_the_scan_by_hand()
    {
        var check = Assert.Single(await All(new RepoHookState("demo", "/work/demo", HookKind.Foreign)), static c => c.Name == "pre-commit:demo");

        Assert.Equal(CheckState.Warn, check.State);
        Assert.Contains("scrub --staged", check.Fix, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Each_repo_gets_its_own_line()
    {
        var checks = await All(new RepoHookState("a", "/work/a", HookKind.V3), new RepoHookState("b", "/work/b", HookKind.None));

        Assert.Equal(["pre-commit:a", "pre-commit:b"], checks.Where(static c => c.Name.StartsWith("pre-commit:", StringComparison.Ordinal)).Select(static c => c.Name), StringComparer.Ordinal);
    }

    private sealed class FixedHooks(RepoHookState[] states) : IRepoHookStates
    {
        public ValueTask<IReadOnlyList<RepoHookState>> Read(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<RepoHookState>>(states);
    }
}
