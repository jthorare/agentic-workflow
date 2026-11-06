using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Connectors.Core.GitHub;

namespace Aw.Connectors.Infrastructure.GitHub;

/// <summary>Reads GitHub's replies: headers, the Link header, and the entries of the issue, comment and pull request replies.</summary>
internal static partial class GitHubWire
{
    public static string? Header(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    public static Uri? NextLink(HttpResponseMessage response)
    {
        var link = Header(response, "Link");
        return link is not null && NextRel.Match(link) is { Success: true } m && Uri.TryCreate(m.Groups["url"].Value, UriKind.Absolute, out var next) ? next : null;
    }

    public static bool SameOrigin(Uri a, Uri b)
        => string.Equals(a.Scheme, b.Scheme, StringComparison.OrdinalIgnoreCase) && string.Equals(a.Host, b.Host, StringComparison.OrdinalIgnoreCase) && a.Port == b.Port;

    /// <summary>Reads one entry; null when it lacks the id, number or a date the cursor needs.</summary>
    public static GitHubIssue? ReadIssue(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var (id, number, created, updated) = (Long(e, "id"), Long(e, "number"), Date(e, "created_at"), Date(e, "updated_at"));
        if (id is null || number is null || created is null || updated is null)
        {
            return null;
        }

        return new GitHubIssue(
            id.Value, (int)number.Value, e.TryGetProperty("pull_request", out _), Str(e, "title"), Str(e, "body"), Str(e, "state"), Author(e), Str(e, "html_url"),
            created.Value, updated.Value, Date(e, "closed_at"), Labels(e), (int)Math.Clamp(Long(e, "comments").GetValueOrDefault(), 0, int.MaxValue));
    }

    /// <summary>Reads one comment; null when it lacks the id, the issue it is on or a date.</summary>
    public static GitHubComment? ReadComment(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var (id, created, updated) = (Long(e, "id"), Date(e, "created_at"), Date(e, "updated_at"));
        var number = IssueNumber(Text(e, "issue_url"));
        return id is null || number is null || created is null || updated is null
            ? null
            : new GitHubComment(id.Value, number.Value, Author(e), Str(e, "body"), created.Value, updated.Value);
    }

    /// <summary>Reads the path of one entry of a pull request's file list; null when it has none.</summary>
    public static string? ReadPath(JsonElement e)
        => e.ValueKind == JsonValueKind.Object && Text(e, "filename") is { Length: > 0 } name ? name : null;

    /// <summary>Reads the merge state of a single pull request.</summary>
    public static GitHubPullDetail ReadPull(JsonElement e, IReadOnlyList<string> paths)
        => new(Flag(e, "merged"), Flag(e, "draft"), Date(e, "merged_at"), paths, RequestedLogins(e), e.TryGetProperty("head", out var head) && head.ValueKind == JsonValueKind.Object ? Text(head, "sha") : null);

    /// <summary>Reduces a pull request's review list to each login's latest review: the one with the newest 'submitted_at' (the later entry on a tie), pending and dismissed reviews (GitHub voided the latter) and ones without a login or commit skipped. A later COMMENTED review replaces an earlier APPROVED one: spec §5.6 counts the latest review whatever its state. Ordered by login.</summary>
    public static IReadOnlyList<(string Login, string Commit)> LatestReviews(IEnumerable<JsonElement> reviews)
    {
        Dictionary<string, (string Commit, DateTimeOffset At)> latest = new(StringComparer.Ordinal);
        foreach (var review in reviews)
        {
            if (review.ValueKind != JsonValueKind.Object || IsSkippedState(Text(review, "state"))
                || Author(review) is not { Length: > 0 } login || Text(review, "commit_id") is not { Length: > 0 } commit)
            {
                continue;
            }

            var at = Date(review, "submitted_at") ?? DateTimeOffset.MinValue;
            if (!latest.TryGetValue(login, out var seen) || at >= seen.At)
            {
                latest[login] = (commit, at);
            }
        }

        return [.. latest.OrderBy(static kv => kv.Key, StringComparer.Ordinal).Select(static kv => (kv.Key, kv.Value.Commit))];
    }

    /// <summary>Reads 'ahead_by' from a compare reply, or null when it has none.</summary>
    public static int? AheadBy(JsonElement e)
        => e.ValueKind == JsonValueKind.Object && Long(e, "ahead_by") is { } n ? (int)Math.Clamp(n, 0, int.MaxValue) : null;

    private static bool IsSkippedState(string? state)
        => string.Equals(state, "PENDING", StringComparison.OrdinalIgnoreCase) || string.Equals(state, "DISMISSED", StringComparison.OrdinalIgnoreCase);

    // A team request is a 'requested_teams' entry with no login; only people are listed here.
    private static List<string> RequestedLogins(JsonElement e)
        => e.TryGetProperty("requested_reviewers", out var people) && people.ValueKind == JsonValueKind.Array
            ? [.. people.EnumerateArray().Select(static p => p.ValueKind == JsonValueKind.Object ? Text(p, "login") : null).OfType<string>().Where(static l => l.Length > 0)]
            : [];

    private static bool Flag(JsonElement e, string name)
        => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;

    private static int? IssueNumber(string? issueUrl)
    {
        var last = issueUrl?[(issueUrl.LastIndexOf('/') + 1)..];
        return int.TryParse(last, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    private static long? Long(JsonElement e, string name)
        => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var v) ? v : null;

    private static string? Text(JsonElement e, string name)
        => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static string Str(JsonElement e, string name) => Text(e, name) ?? string.Empty;

    private static DateTimeOffset? Date(JsonElement e, string name)
        => DateTimeOffset.TryParse(Text(e, name), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d) ? d : null;

    private static string? Author(JsonElement e)
        => e.TryGetProperty("user", out var user) && user.ValueKind == JsonValueKind.Object ? Text(user, "login") : null;

    private static List<string> Labels(JsonElement e)
    {
        List<string> names = [];
        if (e.TryGetProperty("labels", out var labels) && labels.ValueKind == JsonValueKind.Array)
        {
            names.AddRange(labels.EnumerateArray().Select(static l => l.ValueKind == JsonValueKind.String ? l.GetString() : Text(l, "name")).OfType<string>());
        }

        return names;
    }

    [GeneratedRegex("<(?<url>[^>]+)>\\s*;\\s*rel=\"next\"", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex NextRel { get; }
}
