using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Sandbox.Denials;

namespace Aw.Sindri.Tests.Sandbox.Denials;

/// <summary>The fix by kind (sandbox spec §3.6, R3-07): network and not-bound home paths get Allow; a path on the deny list gets a reason and no button; a policy refusal goes to your own terminal.</summary>
public sealed class DenialFixesTests
{
    private static readonly string[] _bannedOwnerWords = ["scope", "plan", "canary", "confined"];
    private static readonly DateTimeOffset _at = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    private static DenialEntry Entry(DenialKind kind, string target, string rule) => new(kind, target, rule, 1, _at, _at);

    [Theory, Trait("Lane", "unit")]
    [InlineData(DenialKind.Network, "github.com", DenialRules.NoGroup, DenialFix.Allow)]
    [InlineData(DenialKind.Path, "/home/o/.cache/tool", DenialRules.NotBound, DenialFix.Allow)]
    [InlineData(DenialKind.Policy, "git push", DenialRules.InnerPolicy, DenialFix.OwnTerminal)]
    [InlineData(DenialKind.Policy, "pre-commit", DenialRules.HookFailOpen, DenialFix.OwnTerminal)]
    public void The_fix_follows_the_kind(DenialKind kind, string target, string rule, DenialFix fix)
    {
        var decided = DenialFixes.Decide(Entry(kind, target, rule));

        Assert.Equal(fix, decided.Fix);
        Assert.Null(decided.Reason);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("/var/run/docker.sock")]
    [InlineData("/run/podman/podman.sock")]
    [InlineData("/home/o/.ssh/id_ed25519")]
    [InlineData("/etc/ssh/ssh_host_rsa_key")]
    [InlineData("/home/o/.agentic-workflow/sindri-app/ledger.db")]
    [InlineData("/somewhere/else")]
    public void A_path_on_the_deny_list_is_never_allowed_and_says_why(string path)
    {
        var decided = DenialFixes.Decide(Entry(DenialKind.Path, path, DenialRules.DenyList));

        Assert.Equal(DenialFix.Never, decided.Fix);
        Assert.False(string.IsNullOrWhiteSpace(decided.Reason));
        Assert.All(_bannedOwnerWords, w => Assert.DoesNotContain(w, decided.Reason, StringComparison.OrdinalIgnoreCase));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("10.0.0.5", DenialRules.PrivateAddress)]
    [InlineData("registry.npmjs.org", DenialRules.Cap)]
    public void A_private_address_or_a_hit_cap_is_not_a_missing_host(string host, string rule)
    {
        var decided = DenialFixes.Decide(Entry(DenialKind.Network, host, rule));

        Assert.Equal(DenialFix.Never, decided.Fix);
        Assert.NotNull(decided.Reason);
    }
}
