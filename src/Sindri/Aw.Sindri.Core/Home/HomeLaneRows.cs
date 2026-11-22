using System.Globalization;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Core.Home;

/// <summary>
/// The lane-fed rows (home spec §3.2, §3.3), from one aw-meta read: 'lane-recycle' for a lane whose verdict is Recycle, 'agent-limited' for a subject whose usage
/// state is Limited, and the merge-queue half of 'pr-ready-merge' for each entry of the merge queue. A name that can't be a row id (a space in it) makes no row.
/// </summary>
public static class HomeLaneRows
{
    /// <summary>The collector's name, which is each row's source.</summary>
    public const string SourceName = "home-lanes";

    /// <summary>The weight of 'at the recycle mark' and 'limited' (home spec §4.2 defaults).</summary>
    public const int LaneWeight = 4;

    /// <summary>The weight of 'ready to merge'.</summary>
    public const int ReadyWeight = 5;

    /// <summary>The kinds the collector raises.</summary>
    public static IReadOnlySet<HomeKind> Kinds { get; } = new HashSet<HomeKind> { HomeKind.LaneRecycle, HomeKind.AgentLimited, HomeKind.PrReadyMerge };

    /// <summary>The rows for <paramref name="team"/>: recycle rows first (lane order), then limited rows, then the merge queue (queue order).</summary>
    /// <param name="team">The read.</param>
    /// <param name="repo">The team repository's 'owner/name', or null when it is not known: a queue row's id is then 'lane:ready:&lt;pr&gt;' and it has no PR link.</param>
    /// <param name="readAt">When the collector read aw-meta.</param>
    /// <param name="scrubber">Scrubs aw-meta's text before it reaches a row.</param>
    /// <returns>The rows; none for an unavailable read.</returns>
    public static IReadOnlyList<HomeRaise> From(MetaTeamRead team, string? repo, DateTimeOffset readAt, ITextScrubber scrubber)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(scrubber);
        if (team.Unavailable is not null)
        {
            return [];
        }

        var source = new HomeSource(SourceName, readAt, Connection: null);
        var rows = new List<HomeRaise>();
        foreach (var lane in team.Lanes.Where(static l => string.Equals(l.Verdict, "Recycle", StringComparison.Ordinal)))
        {
            if (Recycle(lane, team.MergeQueue, repo, source, scrubber) is { } row)
            {
                rows.Add(row);
            }
        }

        foreach (var usage in team.Usage.Where(static u => string.Equals(u.State, "Limited", StringComparison.Ordinal)))
        {
            if (Limited(usage, source, scrubber) is { } row)
            {
                rows.Add(row);
            }
        }

        foreach (var queued in team.MergeQueue)
        {
            if (Ready(queued, repo, source, scrubber) is { } row)
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    /// <summary>The id of a merge-queue row: 'gh:ready:&lt;owner/name&gt;#&lt;pr&gt;' when the repository is known (the forge half uses the same id, so one PR is one row), else 'lane:ready:&lt;pr&gt;'.</summary>
    /// <param name="repo">The repository's 'owner/name', or null.</param>
    /// <param name="pr">The pull request number.</param>
    /// <returns>The id text (a row id and an edge end).</returns>
    private static string ReadyKey(string? repo, int pr) =>
        repo is null ? string.Create(CultureInfo.InvariantCulture, $"lane:ready:{pr}") : string.Create(CultureInfo.InvariantCulture, $"gh:ready:{repo}#{pr}");

    /// <summary>The why chip of a lane at its mark: the size rounded down to 10k ('at 210k'), so a lane above the mark changes its row every 10k tokens, not every minute.</summary>
    /// <param name="tokens">The context size, or null.</param>
    /// <returns>The chip text.</returns>
    public static string RecycleWhy(long? tokens) =>
        tokens is { } t ? string.Create(CultureInfo.InvariantCulture, $"at {t / 10_000 * 10}k") : "at the mark";

    private static HomeRaise? Recycle(MetaLane lane, IReadOnlyList<MetaQueuedPr> queue, string? repo, HomeSource source, ITextScrubber scrubber)
    {
        var name = scrubber.Scrub(lane.Lane);
        if (!HomeRowId.TryFrom($"lane:recycle:{name}", out var id))
        {
            return null;
        }

        var size = lane.Tokens is { } t ? string.Create(CultureInfo.InvariantCulture, $"at {t / 1000}k") : "at the mark";
        IReadOnlyList<HomeLink> links = lane.Session is { } session
            ? [new HomeLink("lane", name, name), new HomeLink("session", scrubber.Scrub(session), Title: null)]
            : [new HomeLink("lane", name, name)];

        // why: the PRs this lane sent 'ready' wait on it (item-of): the ranker counts them as what the recycle row blocks
        IReadOnlyList<HomeEdge> edges = lane.SessionName is { } tmux
            ? [.. queue.Where(q => string.Equals(q.From, tmux, StringComparison.Ordinal)).Select(q => new HomeEdge(id.Value, ReadyKey(repo, q.Pr), HomeEdgeKind.ItemOf))]
            : [];
        var row = Row(new RowWords(id, HomeKind.LaneRecycle, $"{name}: {size}, recycle from a digest", RecycleWhy(lane.Tokens), name, $"lane {name}", LaneWeight), links, ["copy-digest-request", "open-session"], source);
        return new HomeRaise(row, edges);
    }

    private static HomeRaise? Limited(MetaUsage usage, HomeSource source, ITextScrubber scrubber)
    {
        var name = scrubber.Scrub(usage.Subject);
        if (!HomeRowId.TryFrom($"lane:limited:{name}", out var id))
        {
            return null;
        }

        var limit = usage.Kind switch
        {
            "Session" => "session limit",
            "Weekly" => "weekly limit",
            _ => "usage limit",
        };
        var resets = usage.ResetsAt is { } at ? "resets " + at.UtcDateTime.ToString("MMM d HH:mm 'UTC'", CultureInfo.InvariantCulture) : "reset time unknown";
        var from = usage.Agent is { } agent ? scrubber.Scrub(agent) : $"lane {name}";
        var row = Row(new RowWords(id, HomeKind.AgentLimited, $"{name}: stopped at its {limit}, {resets}", "limited", name, from, LaneWeight), [new HomeLink("lane", name, name)], ["open-session"], source);
        return new HomeRaise(row, Edges: []);
    }

    private static HomeRaise? Ready(MetaQueuedPr queued, string? repo, HomeSource source, ITextScrubber scrubber)
    {
        if (queued.Pr <= 0 || !HomeRowId.TryFrom(ReadyKey(repo, queued.Pr), out var id))
        {
            return null;
        }

        var number = queued.Pr.ToString(CultureInfo.InvariantCulture);
        IReadOnlyList<HomeLink> links = repo is null ? [] : [new HomeLink("pr", $"gh:pr:{repo}#{number}", $"PR {number}")];

        // why: while meta is already merging it (GitHub's merge queue holds it) the row offers no second Merge (spec §5.1)
        var merging = queued.Github is { } github ? $"meta is merging ({scrubber.Scrub(github)})" : null;
        var row = Row(new RowWords(id, HomeKind.PrReadyMerge, $"PR {number} is ready to merge", "ready", "#" + number, "meta queue", ReadyWeight), links, merging is null ? ["merge", "open"] : ["open"], source) with
        {
            WaitsOn = merging ?? "you",
        };
        return new HomeRaise(row, Edges: []);
    }

    private static HomeRow Row(RowWords text, IReadOnlyList<HomeLink> links, IReadOnlyList<string> actions, HomeSource source) =>
        new(
            text.Id,
            Version: 1,
            text.Kind,
            HomeGroup.NeedsYou,
            text.Title,
            [new HomeFact(HomeFactKind.Why, text.Why), new HomeFact(HomeFactKind.Object, text.ObjectRef), new HomeFact(HomeFactKind.From, text.From)],
            WaitsOn: "you",
            source.ReadAt,
            new HomeRank(Blocks: 0, text.Weight, SinceRank: 0, Position: 0),
            BlocksList: [],
            links,
            HomeState.Open,
            SnoozeUntil: null,
            DoneAt: null,
            actions,
            source);

    // The words of one row: its id and kind, the title, the three facts and the reason's weight.
    private sealed record RowWords(HomeRowId Id, HomeKind Kind, string Title, string Why, string ObjectRef, string From, int Weight);
}
