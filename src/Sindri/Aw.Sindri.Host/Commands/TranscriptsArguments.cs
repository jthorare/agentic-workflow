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
        var flags = new Flags(now.AddDays(-DefaultDays));
        for (var i = 0; i < args.Length; i++)
        {
            var value = i + 1 < args.Length ? args[i + 1] : null;
            switch (flags.Take(args[i], value, now))
            {
                case Taken.Flag:
                    break;
                case Taken.FlagAndValue:
                    i++;
                    break;
                case Taken.Unknown:
                default:
                    return SindriErrors.InvalidInput.With($"transcripts list can't read '{args[i]}'{(value is null ? string.Empty : $" {value}")} ({ListUsage})");
            }
        }

        return (new SessionQuery(flags.Since) { Provider = flags.Provider, Repo = flags.Repo, IncludeSubagents = flags.Subagents }, flags.Json);
    }

    /// <summary>'7d', a date (midnight UTC) or an RFC 3339 time; null for anything else.</summary>
    internal static DateTimeOffset? Since(string? text, DateTimeOffset now)
    {
        if (text is null)
        {
            return null;
        }

        if (Days.Match(text) is { Success: true } days)
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
    private static partial Regex Days { get; }

    private enum Taken
    {
        Unknown,
        Flag,
        FlagAndValue,
    }

    // The flags read so far; Take says how many arguments a flag used, or Unknown for one it can't read.
    private sealed class Flags(DateTimeOffset since)
    {
        public DateTimeOffset Since { get; private set; } = since;

        public TranscriptProvider? Provider { get; private set; }

        public string? Repo { get; private set; }

        public bool Subagents { get; private set; }

        public bool Json { get; private set; }

        public Taken Take(string flag, string? value, DateTimeOffset now)
        {
            switch (flag)
            {
                case "--json":
                    Json = true;
                    return Taken.Flag;
                case "--subagents":
                    Subagents = true;
                    return Taken.Flag;
                case "--since" when TranscriptsArguments.Since(value, now) is { } at:
                    Since = at;
                    return Taken.FlagAndValue;
                case "--provider" when TranscriptProviderNames.TryParse(value, out var provider):
                    Provider = provider;
                    return Taken.FlagAndValue;
                case "--repo" when value is { Length: > 1 } path && path.StartsWith('/') && path.TrimEnd('/').Length > 0:
                    Repo = path.TrimEnd('/');
                    return Taken.FlagAndValue;
                default:
                    return Taken.Unknown;
            }
        }
    }
}
