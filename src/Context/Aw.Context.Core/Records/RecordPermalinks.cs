using Aw.Context.Core.Linear;
using Aw.Context.Core.Trust;

namespace Aw.Context.Core.Records;

/// <summary>Where a stored record opens outside Sindri: its permalink and its other names, read one way for every caller (#1702).</summary>
public static class RecordPermalinks
{
    /// <summary>A stored record's link and aliases: the permalink when it scrubs unchanged and its host is allowed, and the Linear identifier when it has one.</summary>
    /// <param name="record">The stored record.</param>
    /// <param name="linearTeams">The profile's Linear team keys.</param>
    /// <param name="egress">The per-field scrubber.</param>
    /// <returns>The link, null when there is none to give, and the aliases that scrub unblocked.</returns>
    public static RecordSourceLink Of(StoredRecord record, IReadOnlySet<string> linearTeams, IEgressScrubber egress)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(egress);
        var link = Uri.TryCreate(record.Url, UriKind.Absolute, out var parsed) ? Permalink(parsed, egress) : null;
        var aliases = LinearIdentifiers.AliasesOf(record.Key, record.Url, linearTeams)
            .Select(egress.Scrub).Where(s => !s.Blocked).Select(s => s.Text).ToList();
        return new RecordSourceLink(link, aliases);
    }

    /// <summary>
    /// The link, scrubbed as written and as it reads unescaped, then allowed by host; one the scrub changes at all
    /// (redacted or blocked) is dropped, never rewritten: a link with a placeholder in it leads nowhere and could mislead.
    /// </summary>
    /// <param name="link">A source's permalink, or null.</param>
    /// <param name="egress">The per-field scrubber.</param>
    /// <returns>The link or null.</returns>
    public static Uri? Permalink(Uri? link, IEgressScrubber egress)
    {
        ArgumentNullException.ThrowIfNull(egress);
        return link is not null && Unchanged(link.OriginalString, egress) && Unchanged(Uri.UnescapeDataString(link.OriginalString), egress) ? Permalinks.Allowed(link) : null;
    }

    private static bool Unchanged(string value, IEgressScrubber egress) =>
        egress.Scrub(value) is { Blocked: false } scrubbed && string.Equals(scrubbed.Text, value, StringComparison.Ordinal);
}
