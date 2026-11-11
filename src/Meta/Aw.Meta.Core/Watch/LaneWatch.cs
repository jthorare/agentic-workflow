namespace Aw.Meta.Core.Watch;

/// <summary>
/// Turns each poll's lanes with no live session into one LANE DOWN line when a lane that had a session loses it (two polls
/// agree), and one LANE UP line when it has a session again (#2856: an OOM kill stopped a lane's whole tmux pane and nobody saw).
/// A lane never seen with a session is not reported: a team that is still starting prints nothing.
/// </summary>
/// <param name="up">The lanes last seen with a live session.</param>
/// <param name="down">The lanes reported down.</param>
public sealed class LaneWatch(IReadOnlySet<string> up, IReadOnlySet<string> down)
{
    /// <summary>The lanes last seen with a live session.</summary>
    public IReadOnlySet<string> Up { get; } = up;

    /// <summary>The lanes reported down.</summary>
    public IReadOnlySet<string> Down { get; } = down;

    /// <summary>Reads the kept form.</summary>
    /// <param name="text">The stored text ("up=a,b;down=c"), or null when none was kept.</param>
    /// <returns>The kept state; null for none or anything unreadable (the next poll then records the lanes without a line).</returns>
    public static LaneWatch? Parse(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var parts = text.Split(';');
        if (parts.Length != 2 || !parts[0].StartsWith("up=", StringComparison.Ordinal) || !parts[1].StartsWith("down=", StringComparison.Ordinal))
        {
            return null;
        }

        return new LaneWatch(Names(parts[0]["up=".Length..]), Names(parts[1]["down=".Length..]));
    }

    /// <summary>The kept form: "up=a,b;down=c", each list in name order.</summary>
    /// <returns>The text.</returns>
    public string Format() => $"up={string.Join(',', Up.Order(StringComparer.Ordinal))};down={string.Join(',', Down.Order(StringComparer.Ordinal))}";

    /// <summary>Compares a poll with the kept state.</summary>
    /// <param name="kept">The kept state; null records this poll's live lanes as up and reports nothing.</param>
    /// <param name="pending">The lanes the previous poll of this watch found without a session but did not report yet.</param>
    /// <param name="lanes">The configured lanes (the meta lane left out).</param>
    /// <param name="missing">This poll's lanes with no live session.</param>
    /// <returns>The lines to report, the new state, and the lanes waiting for a second poll.</returns>
    public static (IReadOnlyList<string> Lines, LaneWatch State, IReadOnlySet<string> Pending) Step(LaneWatch? kept, IReadOnlySet<string> pending, IReadOnlyList<string> lanes, IReadOnlyList<string> missing)
    {
        ArgumentNullException.ThrowIfNull(pending);
        ArgumentNullException.ThrowIfNull(lanes);
        ArgumentNullException.ThrowIfNull(missing);
        var gone = missing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var next = new Next(kept, pending);
        foreach (var lane in lanes)
        {
            if (gone.Contains(lane))
            {
                next.Gone(lane);
            }
            else
            {
                next.Live(lane);
            }
        }

        // A lane taken out of the team config is forgotten.
        next.Up.IntersectWith(lanes);
        next.Down.IntersectWith(lanes);
        return (next.Lines, new LaneWatch(next.Up, next.Down), next.Waiting);
    }

    // The state one poll builds, lane by lane.
    private sealed class Next(LaneWatch? kept, IReadOnlySet<string> pending)
    {
        internal HashSet<string> Up { get; } = new(kept?.Up ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        internal HashSet<string> Down { get; } = new(kept?.Down ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        internal HashSet<string> Waiting { get; } = new(StringComparer.OrdinalIgnoreCase);

        internal List<string> Lines { get; } = [];

        internal void Live(string lane)
        {
            _ = Up.Add(lane);
            if (Down.Remove(lane))
            {
                Lines.Add($"LANE UP {lane}");
            }
        }

        // Only a lane seen with a session counts, and only once a second poll agrees.
        internal void Gone(string lane)
        {
            if (kept is null || !Up.Contains(lane))
            {
                return;
            }

            if (!pending.Contains(lane))
            {
                _ = Waiting.Add(lane);
                return;
            }

            _ = Up.Remove(lane);
            _ = Down.Add(lane);
            Lines.Add($"LANE DOWN {lane}: no live session; restart it with aw-meta-launch --lanes {lane}");
        }
    }

    private static HashSet<string> Names(string list) =>
        list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
}
