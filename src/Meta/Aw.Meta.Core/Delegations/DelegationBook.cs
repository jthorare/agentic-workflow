using System.Globalization;

namespace Aw.Meta.Core.Delegations;

/// <summary>
/// The delegations as the key-value store keeps them: one line per issue, tab-separated
/// "issue, lane, session, time (round-trip), delivered (0|1), claude (0|1), url, title". A line that doesn't read is skipped.
/// </summary>
public static class DelegationBook
{
    private const int Fields = 8;

    /// <summary>Reads the stored text.</summary>
    /// <param name="stored">The stored value; null when nothing was delegated yet.</param>
    /// <returns>The delegations, in the stored order (oldest first).</returns>
    public static IReadOnlyList<Delegation> Parse(string? stored) =>
        stored is null ? [] : [.. stored.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(Line).OfType<Delegation>()];

    /// <summary>Writes the delegations back as text.</summary>
    /// <param name="delegations">The delegations.</param>
    /// <returns>The text to store.</returns>
    public static string Format(IEnumerable<Delegation> delegations)
    {
        ArgumentNullException.ThrowIfNull(delegations);
        return string.Join('\n', delegations.Select(static d => string.Join(
            '\t',
            d.Issue.ToString(CultureInfo.InvariantCulture),
            Clean(d.Lane),
            Clean(d.Session),
            d.At.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            d.Delivered ? "1" : "0",
            d.Claude ? "1" : "0",
            Clean(d.Url),
            Clean(d.Title))));
    }

    /// <summary>The list with <paramref name="delegation"/> added; an earlier delegation of the same issue is replaced (the latest wins).</summary>
    /// <param name="delegations">The current list.</param>
    /// <param name="delegation">The new delegation.</param>
    /// <returns>The new list, the new delegation last.</returns>
    public static IReadOnlyList<Delegation> With(IReadOnlyList<Delegation> delegations, Delegation delegation)
    {
        ArgumentNullException.ThrowIfNull(delegations);
        ArgumentNullException.ThrowIfNull(delegation);
        return [.. delegations.Where(d => d.Issue != delegation.Issue), delegation];
    }

    private static Delegation? Line(string line)
    {
        var parts = line.TrimEnd('\r').Split('\t');
        return parts.Length == Fields
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var issue)
            && DateTimeOffset.TryParse(parts[3], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
            ? new Delegation(issue, parts[6], parts[7], parts[1], parts[2], at) { Delivered = parts[4] == "1", Claude = parts[5] == "1" }
            : null;
    }

    private static string Clean(string text) => text.Replace('\t', ' ').ReplaceLineEndings(" ").Trim();
}
