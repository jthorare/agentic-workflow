using System.Globalization;
using System.Text;
using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Core.Sandbox.Denials;

/// <summary>
/// The text of 'sindri sandbox denials' (sandbox spec §3.6): rows grouped by target and rule across the launches, the most counted first,
/// each with the fix its kind allows (R3-07). The words are "sandbox", "blocked" and "limit".
/// </summary>
public static class DenialsRenderer
{
    private const string NoneText = "Nothing was blocked by the sandbox.\n";

    /// <summary>The denials as text.</summary>
    /// <param name="files">The counters, one per launch.</param>
    /// <returns>The text, one row per target and rule, each followed by its fix line.</returns>
    public static string Render(IReadOnlyList<DenialFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var rows = files.SelectMany(static f => f.Entries)
            .GroupBy(static e => (e.Kind, e.Target, e.Rule))
            .Select(static g => (g.Key.Kind, g.Key.Target, g.Key.Rule, Count: g.Sum(static e => e.Count)))
            .OrderByDescending(static r => r.Count).ThenBy(static r => r.Target, StringComparer.Ordinal).ThenBy(static r => r.Rule, StringComparer.Ordinal)
            .ToList();
        var overflow = files.SelectMany(static f => f.Overflow).GroupBy(static o => o.Kind).Select(static g => (Kind: g.Key, Count: g.Sum(static o => o.Count)))
            .OrderBy(static o => o.Kind).ToList();
        if (rows.Count == 0 && overflow.Count == 0)
        {
            return NoneText;
        }

        var text = new StringBuilder();
        foreach (var (kind, target, rule, count) in rows)
        {
            _ = text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{Name(kind),-8} {target}  x{count}  ({rule})"));
            _ = text.Append("         ").AppendLine(Fix(kind, target, rule));
        }

        foreach (var (kind, count) in overflow)
        {
            _ = text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"+{count} more {Name(kind)} blocks not counted by target"));
        }

        return text.ToString();
    }

    /// <summary>The fix line of one row, by kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="target">The host, path or tool.</param>
    /// <param name="rule">The rule id.</param>
    /// <returns>The sentence.</returns>
    public static string Fix(DenialKind kind, string target, string rule)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(rule);
        return kind switch
        {
            DenialKind.Network when string.Equals(rule, DenialRules.PrivateAddress, StringComparison.Ordinal) => $"never allowed: {target} points at this machine or its own network",
            DenialKind.Network when string.Equals(rule, DenialRules.Cap, StringComparison.Ordinal) => "limit reached: this run used up its connections or bytes",
            DenialKind.Network => $"allow {target} for this repo: add it to sandbox.repos.<repo>.hosts",
            DenialKind.Path when string.Equals(rule, DenialRules.NotBound, StringComparison.Ordinal) => $"allow {target} for this repo: sandbox.repos.<repo>.homeAllow",
            DenialKind.Path => "never allowed: the sandbox always blocks this path (Sindri's own state, the Docker socket and keys)",
            DenialKind.Policy when string.Equals(rule, DenialRules.HookFailOpen, StringComparison.Ordinal) => $"the hook {target} did not run; the rest of the sandbox still held",
            DenialKind.Policy when target.Contains("git push", StringComparison.Ordinal) => "blocked git push; push from your own terminal, or let Sindri send it",
            DenialKind.Policy or _ => $"blocked {target} by the agent's own policy",
        };
    }

    private static string Name(DenialKind kind) => kind switch
    {
        DenialKind.Network => "network",
        DenialKind.Path => "path",
        DenialKind.Policy or _ => "policy",
    };
}
