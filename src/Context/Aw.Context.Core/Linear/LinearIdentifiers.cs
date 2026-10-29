using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Linear;

/// <summary>The tracker identifier of a Linear issue ('SPL-103'), read from its stored permalink (https://linear.app/&lt;workspace&gt;/issue/&lt;TEAM-N&gt;[/slug]).</summary>
public static class LinearIdentifiers
{
    private const string Host = "linear.app";

    /// <summary>The identifier the permalink names, when its team is one the profile reads.</summary>
    /// <param name="url">The stored permalink, or null.</param>
    /// <param name="teams">The profile's Linear team keys.</param>
    /// <returns>'TEAM-N' (digits only after the dash), or null when the url isn't an https linear.app issue link or its team isn't in <paramref name="teams"/>.</returns>
    public static string? FromPermalink(string? url, IReadOnlySet<string> teams)
    {
        ArgumentNullException.ThrowIfNull(teams);
        var segment = IssueSegment(url);
        var dash = segment?.LastIndexOf('-') ?? -1;
        return segment is not null && dash > 0 && teams.Contains(segment[..dash]) && segment.Length > dash + 1 && segment[(dash + 1)..].All(char.IsAsciiDigit) ? segment : null;
    }

    /// <summary>A stored record's aliases: a Linear issue's tracker identifier, read from its permalink; nothing for any other record.</summary>
    /// <param name="key">The stored record's key.</param>
    /// <param name="url">Its stored permalink, or null.</param>
    /// <param name="teams">The profile's Linear team keys.</param>
    /// <returns>One identifier, or none.</returns>
    public static string[] AliasesOf(RecordKey key, string? url, IReadOnlySet<string> teams) =>
        LinearRecordKeys.IsLinear(key) && LinearRecordKeys.KindOf(key) == RecordKind.Issue && FromPermalink(url, teams) is { } identifier
            ? [identifier]
            : [];

    /// <summary>The path segment after '/issue/' of an https linear.app link, or null.</summary>
    private static string? IssueSegment(string? url)
    {
        if (url is null || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || !string.Equals(uri.Host, Host, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 3 && string.Equals(parts[1], "issue", StringComparison.Ordinal) ? parts[2] : null;
    }
}
