using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Transcripts.Core;
using Aw.Transcripts.Core.Sessions;

namespace Aw.Sindri.Host.Commands;

/// <summary>Parses 'transcripts list' flags: --since &lt;YYYY-MM-DD | RFC 3339 | Nd&gt; (default 7d), --provider, --repo &lt;absolute path&gt;, --subagents, --json.</summary>
internal static partial class TranscriptsArguments
{
    internal const string ListUsage = "usage: sindri transcripts list [--since <YYYY-MM-DD|RFC 3339|7d>] [--provider claude-code|codex|cursor] [--repo PATH] [--subagents] [--json]";
    internal const string ShowUsage = "usage: sindri transcripts show <ref> [--json]";
    private const int DefaultDays = 7;

    /// <summary>The query and --json, or SND-CLI-003 naming the bad flag.</summary>
    internal static Result<(SessionQuery Query, bool Json)> List(string[] args, DateTimeOffset now)
    {
        var since = now.AddDays(-DefaultDays);
        TranscriptProvider? provider = null;
        string? repo = null;
        var subagents = false;
        var json = false;
        for (var i = 0; i < args.Length; i++)
        {
            var value = i + 1 < args.Length ? args[i + 1] : null;
            switch (args[i])
            {
                case "--json":
                    json = true;
                    continue;
                case "--subagents":
                    subagents = true;
                    continue;
                case "--since" when Since(value, now) is { } at:
                    since = at;
                    break;
                case "--provider" when TranscriptProviderNames.TryParse(value, out var p):
                    provider = p;
                    break;
                case "--repo" when value is { Length: > 1 } path && path.StartsWith('/'):
                    repo = path.TrimEnd('/');
                    break;
                default:
                    return SindriErrors.InvalidInput.With($"transcripts list can't read '{args[i]}'{(value is null ? string.Empty : $" {value}")} ({ListUsage})");
            }

            i++;
        }

        return (new SessionQuery(since) { Provider = provider, Repo = repo, IncludeSubagents = subagents }, json);
    }

    /// <summary>'7d', a date (midnight UTC) or an RFC 3339 time; null for anything else.</summary>
    internal static DateTimeOffset? Since(string? text, DateTimeOffset now)
    {
        if (text is null)
        {
            return null;
        }

        if (Days().Match(text) is { Success: true } days)
        {
            var n = int.Parse(days.Groups["n"].Value, CultureInfo.InvariantCulture);
            return n >= 1 ? now.AddDays(-n) : null;
        }

        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        }

        return text.Contains('T', StringComparison.Ordinal) && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at) ? at : null;
    }

    [GeneratedRegex("^(?<n>[0-9]{1,4})d\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Days();
}
