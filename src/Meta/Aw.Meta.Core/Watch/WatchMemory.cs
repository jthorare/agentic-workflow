using System.Globalization;
using System.Text;

namespace Aw.Meta.Core.Watch;

/// <summary>
/// The pull requests and main's sha the last poll settled on, kept between watch runs so a run's first poll diffs against the last
/// poll of the run before (a change that lands in the gap is reported, not swallowed as a baseline). Kept per repo.
/// </summary>
public static class WatchMemory
{
    private const string RepoPrefix = "repo=";
    private const string MainPrefix = "main=";
    private const string PrPrefix = "pr|";
    private const string NoValue = "-";

    /// <summary>The kept form: "repo=", "main=" and one "pr|number|state|draft|head|mergeable" line per pull request.</summary>
    /// <param name="repo">The repo the snapshot is of.</param>
    /// <param name="state">The state after a poll.</param>
    /// <returns>The text.</returns>
    public static string Format(string repo, WatchState state)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(state);
        var text = new StringBuilder();
        _ = text.Append(RepoPrefix).Append(repo.ReplaceLineEndings(" ")).Append('\n');
        _ = text.Append(MainPrefix).Append(state.MainSha ?? string.Empty).Append('\n');
        foreach (var (number, pr) in state.Prs.OrderBy(p => p.Key))
        {
            _ = text.Append(PrPrefix)
                .Append(number.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(((int)pr.State).ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(pr.IsDraft ? '1' : '0').Append('|')
                .Append(pr.Head).Append('|')
                .Append(pr.Mergeable ?? NoValue).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>Reads the kept form into the state a run starts from.</summary>
    /// <param name="text">The stored text, or null when none was kept.</param>
    /// <param name="repo">The repo being watched: a snapshot of another repo is not used.</param>
    /// <param name="resources">The disk and memory states an earlier watch reported.</param>
    /// <returns>The state to start from; the state before any poll for null, another repo's snapshot or anything unreadable.</returns>
    public static WatchState Parse(string? text, string repo, WatchResources resources)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(resources);
        var none = WatchState.Continuing(resources);
        if (string.IsNullOrEmpty(text))
        {
            return none;
        }

        string? keptRepo = null;
        string? main = null;
        var prs = new Dictionary<int, WatchedPr>();
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith(RepoPrefix, StringComparison.Ordinal))
            {
                keptRepo = line[RepoPrefix.Length..];
            }
            else if (line.StartsWith(MainPrefix, StringComparison.Ordinal))
            {
                main = line[MainPrefix.Length..];
            }
            else if (line.StartsWith(PrPrefix, StringComparison.Ordinal))
            {
                if (ParsePr(line[PrPrefix.Length..]) is not { } parsed)
                {
                    return none;
                }

                prs[parsed.Number] = parsed.Pr;
            }
        }

        return keptRepo == repo.ReplaceLineEndings(" ") && !string.IsNullOrEmpty(main)
            ? none with { Initialized = true, Prs = prs, MainSha = main }
            : none;
    }

    private static (int Number, WatchedPr Pr)? ParsePr(string text)
    {
        var parts = text.Split('|');
        if (parts.Length != 5
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var state)
            || !Enum.IsDefined((PrState)state))
        {
            return null;
        }

        return (number, new WatchedPr((PrState)state, parts[2] == "1", parts[3], parts[4] == NoValue ? null : parts[4], Pending: null, PendingPolls: 0));
    }
}
