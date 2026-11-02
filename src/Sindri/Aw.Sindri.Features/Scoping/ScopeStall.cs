using System.Globalization;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// A lens that comes back degraded the same way after a regather (#1239): the second gather can't change a limit that doesn't move between runs, so the check
/// reports 'stalled' instead of 'regather', with a finding that names the lens, the source, what its row says and the fix. Evidence then reports 'converged' on
/// its first round when only those findings fail, and the owner decides with the packet in front of them.
/// </summary>
internal static class ScopeStall
{
    /// <summary>Whether the check's findings are SCP-COV-002 alone and are the very findings (same lens, same text) the check's last round logged.</summary>
    /// <param name="facts">The check's facts, which hold the round log so far.</param>
    /// <param name="findings">What the check found this run.</param>
    /// <returns>True when a regather would only repeat the last one.</returns>
    internal static bool Repeated(ScopeFacts facts, IReadOnlyList<ScopeFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);
        return OnlyCov2(findings) && RoundLog.LastFindings(facts, "check").SetEquals(findings.Select(static f => (RoundLog.KeyOf(f), f.Message)));
    }

    /// <summary>Whether evidence's findings are SCP-COV-002 alone and each is one the check already stalled on.</summary>
    /// <param name="facts">The evidence's facts, which hold the round log so far.</param>
    /// <param name="findings">What evidence found this run.</param>
    /// <returns>True when the owner should decide instead of another gather.</returns>
    internal static bool Held(ScopeFacts facts, IReadOnlyList<ScopeFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);
        var stalled = RoundLog.KeysOf(facts, RoundLog.Stalled);
        return OnlyCov2(findings) && findings.All(f => stalled.Contains(RoundLog.KeyOf(f)));
    }

    /// <summary>The findings with a message that names the lens, the degraded source, its state and code or reason, and what the lens found, and a fix that points at the row.</summary>
    /// <param name="facts">The facts, which hold the coverage rows.</param>
    /// <param name="findings">The SCP-COV-002 findings.</param>
    /// <returns>The same findings, named.</returns>
    internal static IReadOnlyList<ScopeFinding> Named(ScopeFacts facts, IReadOnlyList<ScopeFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);
        return [.. findings.Select(f => f with { Message = Message(facts, f), Fix = "Narrow the anchors, or fix what the source's row names (its code or reason), then scope again; or approve the scope with this lens as it is." })];
    }

    private static bool OnlyCov2(IReadOnlyList<ScopeFinding> findings) =>
        findings.Count > 0 && findings.All(static f => string.Equals(f.Rule, ScopeRules.Cov2, StringComparison.Ordinal));

    private static string Message(ScopeFacts facts, ScopeFinding finding)
    {
        var row = ScopeJson.Items(facts["coverage"], "lenses").FirstOrDefault(r => string.Equals(ScopeJson.Str(r, "lens"), finding.Item, StringComparison.Ordinal));
        var count = (ScopeJson.Int(ScopeJson.Obj(row, "found"), "count") ?? 0).ToString(CultureInfo.InvariantCulture);
        var sources = ScopeJson.Items(ScopeJson.Obj(row, "searched"), "sources").OfType<JsonObject>()
            .Where(static s => !string.Equals(ScopeJson.Str(s, "state"), "ok", StringComparison.Ordinal))
            .Select(static s => $"{ScopeJson.Str(s, "id") ?? "source"} is {ScopeJson.Str(s, "state") ?? "not ok"}{Cause(s)}")
            .ToList();
        var what = sources.Count > 0 ? string.Join("; ", sources) : "a source is not ok";
        return $"the {finding.Item} lens: {what}, and a second gather returned the same result (it found {count}); gathering again cannot change it";
    }

    private static string Cause(JsonObject source) =>
        (ScopeJson.Str(source, "code"), ScopeJson.Str(source, "reason")) switch
        {
            ({ Length: > 0 } code, { Length: > 0 } reason) => $" ({code}, {reason})",
            ({ Length: > 0 } code, _) => $" ({code})",
            (_, { Length: > 0 } reason) => $" ({reason})",
            _ => string.Empty,
        };
}
