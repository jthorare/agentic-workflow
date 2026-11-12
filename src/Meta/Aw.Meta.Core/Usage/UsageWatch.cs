using Aw.Meta.Core.Config;

namespace Aw.Meta.Core.Usage;

/// <summary>The watch's usage rules, pure: which lines to print, which lanes to resume and what to remember.</summary>
public static class UsageWatch
{
    /// <summary>The meta's subject name.</summary>
    public const string Meta = "meta";

    /// <summary>The line typed into a lane whose window is back.</summary>
    public const string ResumeLine = "continue — the usage window has reset; resume where you stopped, including paused builders";

    /// <summary>What the meta is told when its window is back.</summary>
    public const string MetaWake = "USAGE OK meta: resume your subagents (SendMessage them) and re-arm the watch";

    /// <summary>Takes one look.</summary>
    /// <param name="memory">What earlier looks announced.</param>
    /// <param name="readings">Each subject's standing now.</param>
    /// <param name="now">The current time.</param>
    /// <param name="config">The margin, probe interval and auto-resume.</param>
    /// <returns>The lines, the new memory, the lanes to resume and the log notes.</returns>
    internal static UsageStep Step(UsageMemory memory, IReadOnlyList<UsageReading> readings, DateTimeOffset now, UsageConfig config)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(readings);
        ArgumentNullException.ThrowIfNull(config);
        var look = new Look(now, config);
        var entries = new Dictionary<string, UsageEntry>(memory.Entries, StringComparer.Ordinal);
        foreach (var reading in readings)
        {
            _ = entries.TryGetValue(reading.Subject, out var entry);
            switch (reading.State)
            {
                case UsageState.Ok when entry is not null:
                    look.Recovered(reading.Subject, entry);
                    _ = entries.Remove(reading.Subject);
                    break;
                case UsageState.Limited when reading.Stop is { } stop:
                    entries[reading.Subject] = look.Limited(reading, stop, entry);
                    break;
                case UsageState.Ok:
                case UsageState.Limited:
                case UsageState.Unknown:
                default:
                    break;
            }
        }

        return new UsageStep(look.Lines, new UsageMemory(entries), look.Resume, look.Notes) { Edges = look.Edges };
    }

    // One look's output and the clock it is judged by.
    private sealed class Look(DateTimeOffset now, UsageConfig config)
    {
        internal List<string> Lines { get; } = [];

        internal List<string> Resume { get; } = [];

        internal List<string> Notes { get; } = [];

        internal List<UsageEdge> Edges { get; } = [];

        internal void Recovered(string subject, UsageEntry entry)
        {
            if (entry.OkAt is null)
            {
                Lines.Add($"USAGE OK {subject}");
                Notes.Add($"usage: {subject} is working again");
                Edges.Add(new UsageEdge(Limited: false, subject, Until: null));
            }
        }

        internal UsageEntry Limited(UsageReading reading, UsageStop stop, UsageEntry? known)
        {
            var entry = known is not null && string.Equals(known.Key, stop.Key, StringComparison.Ordinal) ? known : Announce(reading.Subject, stop);
            var isMeta = string.Equals(reading.Subject, Meta, StringComparison.Ordinal);
            if (Due(entry, stop))
            {
                // The line repeats every probe interval while the stop stands; the event is the first one only.
                if (entry.OkAt is null)
                {
                    Edges.Add(new UsageEdge(Limited: false, reading.Subject, stop.ResetsAt));
                }

                Lines.Add($"USAGE OK {reading.Subject}");
                if (isMeta)
                {
                    Lines.Add(MetaWake);
                    Notes.Add("usage: the meta's window is back; woke it");
                }

                entry = entry with { OkAt = now, Resumed = isMeta };
            }

            if (!isMeta && entry is { OkAt: not null, Resumed: false } && config.AutoResume && reading.Idle)
            {
                Resume.Add(reading.Subject);
                entry = entry with { Resumed = true };
            }

            return entry;
        }

        private UsageEntry Announce(string subject, UsageStop stop)
        {
            var kind = stop.Kind.ToString().ToLowerInvariant();
            Lines.Add($"USAGE LIMITED {subject} {kind} resets {stop.ResetText}");
            Notes.Add($"usage: {subject} hit its {kind} limit, resets {stop.ResetText}");
            Edges.Add(new UsageEdge(Limited: true, subject, stop.ResetsAt));
            return new UsageEntry(stop.Key, now, OkAt: null, Resumed: false);
        }

        // The first time at the reset plus the margin (or a probe interval after the stop was first seen when the reset is unknown); after that every probe interval.
        private bool Due(UsageEntry entry, UsageStop stop)
        {
            var probe = TimeSpan.FromMinutes(config.ProbeMinutes);
            return entry.OkAt is { } last ? now >= last + probe
                : stop.ResetsAt is { } at ? now >= at + TimeSpan.FromSeconds(config.ResumeMarginSeconds)
                : now >= entry.Since + probe;
        }
    }
}
