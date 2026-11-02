using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Sandbox.Denials;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Tests.Sandbox.Denials;

/// <summary>'sindri sandbox denials' text (sandbox spec §3.6): grouped by target and rule, most counted first, each with its fix by kind (R3-07). Pinned by goldens.</summary>
public sealed class DenialsRendererTests
{
    internal const string LaunchOne = "LCH-00000000000000000000000000000001";
    internal const string LaunchTwo = "LCH-00000000000000000000000000000002";

    private static readonly string[] _bannedOwnerWords = ["scope", "plan", "canary", "confined"];
    private static readonly DateTimeOffset _first = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    internal static DenialEntry Entry(DenialKind kind, string target, string rule, int count) => new(kind, target, rule, count, _first, _first.AddMinutes(count));

    internal static DenialFile File(string launch, params DenialEntry[] entries) => new(LaunchId.From(launch), _first.AddHours(1), entries, []);

    private static void Match(string text, string golden)
    {
        Golden.Match(text, typeof(DenialsRendererTests).Assembly, "Sandbox/Denials/Golden/" + golden, Normalization.None);
        Assert.All(_bannedOwnerWords, w => Assert.DoesNotContain(w, text, StringComparison.OrdinalIgnoreCase));
    }

    [Fact, Trait("Lane", "unit")]
    public void No_denials_match_the_none_golden() => Match(DenialsRenderer.Render([]), "denials-none.txt");

    [Fact, Trait("Lane", "unit")]
    public void Some_denials_match_the_some_golden() => Match(
        DenialsRenderer.Render(
        [
            File(
                LaunchOne,
                Entry(DenialKind.Policy, "git push", DenialRules.InnerPolicy, 1),
                Entry(DenialKind.Path, "/home/owner/.ssh/id_ed25519", DenialRules.DenyList, 1),
                Entry(DenialKind.Path, "/home/owner/.cache/tool", DenialRules.NotBound, 2),
                Entry(DenialKind.Network, "github.com", DenialRules.NoGroup, 3)),
        ]),
        "denials-some.txt");

    [Fact, Trait("Lane", "unit")]
    public void Denials_lists_counts_grouped_by_target_and_rule()
    {
        var first = new DenialFile(
            LaunchId.From(LaunchOne),
            _first,
            [
                Entry(DenialKind.Network, "github.com", DenialRules.NoGroup, 5),
                Entry(DenialKind.Network, "registry.npmjs.org", DenialRules.NoGroup, 4),
                Entry(DenialKind.Network, "pypi.org", DenialRules.NoGroup, 3),
                Entry(DenialKind.Path, "/home/owner/.cache/pip", DenialRules.NotBound, 2),
                Entry(DenialKind.Path, "/home/owner/.config/gh", DenialRules.NotBound, 2),
            ],
            [new DenialOverflow(DenialKind.Network, 7)]);
        var second = new DenialFile(
            LaunchId.From(LaunchTwo),
            _first,
            [
                Entry(DenialKind.Network, "github.com", DenialRules.NoGroup, 2),
                Entry(DenialKind.Network, "evil.internal", DenialRules.PrivateAddress, 1),
                Entry(DenialKind.Policy, "gh", DenialRules.InnerPolicy, 2),
                Entry(DenialKind.Policy, "curl", DenialRules.InnerPolicy, 1),
                Entry(DenialKind.Path, "/var/run/docker.sock", DenialRules.DenyList, 1),
                Entry(DenialKind.Policy, "judge", DenialRules.HookFailOpen, 1),
            ],
            [new DenialOverflow(DenialKind.Network, 3), new DenialOverflow(DenialKind.Path, 4)]);

        Match(DenialsRenderer.Render([first, second]), "denials-many.txt");
    }

    [Fact, Trait("Lane", "unit")]
    public void The_same_target_under_two_rules_stays_two_rows()
    {
        var text = DenialsRenderer.Render([File(LaunchOne, Entry(DenialKind.Network, "a.example", DenialRules.NoGroup, 1), Entry(DenialKind.Network, "a.example", DenialRules.Cap, 1))]);

        Assert.Contains("(cap)", text, StringComparison.Ordinal);
        Assert.Contains("(no-group)", text, StringComparison.Ordinal);
    }
}
