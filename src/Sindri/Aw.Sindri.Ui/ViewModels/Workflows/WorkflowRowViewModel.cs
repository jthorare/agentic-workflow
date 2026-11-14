using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One workflow on the list: its name, whether it is on (a glyph and a word), which revision, and who changed it last. It maps served fields to words and decides nothing.</summary>
internal sealed class WorkflowRowViewModel : ObservableObject
{
    internal WorkflowRowViewModel(WorkflowRowView row, TimeProvider clock)
    {
        Slug = row.Slug;
        NameText = row.Name;
        IsActive = row.ActiveRevision is not null;
        HasNewerRevision = row.ActiveRevision is { } active && active < row.LatestRevision;
        var (glyph, word) = Labels.WfState(IsActive);
        (ChipWord, ChipGlyph, ChipToneWord) = (word, glyph, IsActive ? "ok" : "neutral");
        RevisionText = Labels.WfRevision(row.ActiveRevision, row.LatestRevision);
        ChangedText = Labels.WfChangedBy(row.ChangedBy, clock.GetUtcNow() - row.ChangedAt);
        TriggerText = row.Trigger ?? string.Empty;
        IsPaused = row.Paused == true;
        StateText = IsPaused ? "Paused" : IsActive ? "Active" : "Off";
        NeedsLook = IsPaused || string.Equals(row.LastRun?.Status, "failed", StringComparison.Ordinal);
        (LastRunText, LastRunToneWord) = LastRunOf(row, IsPaused, clock);
        NextRunText = NextRunOf(row, IsPaused, clock);
        Activity = row.LastRun?.At ?? row.ChangedAt;
        Strip = StripOf(row, clock);
        Name = Labels.WfRowName(NameText, word.ToLowerInvariant(), RevisionText.ToLowerInvariant(), ChangedText.ToLowerInvariant());
    }

    /// <summary>The trigger in words.</summary>
    public string TriggerText { get; }

    /// <summary>"Failed 3m ago", or "Paused" for a paused workflow; empty when there is nothing to say.</summary>
    public string LastRunText { get; }

    /// <summary>The last run chip's tone: ok, error, attention, running or neutral.</summary>
    public string LastRunToneWord { get; }

    /// <summary>True when the Last run cell has a chip.</summary>
    public bool ShowsLastRun => LastRunText.Length > 0;

    /// <summary>True for a workflow with a schedule that is held.</summary>
    public bool IsPaused { get; }

    /// <summary>"Active", "Paused" or "Off".</summary>
    public string StateText { get; }

    /// <summary>"11:00", "Mon 7:00", "-" for a paused one, or empty.</summary>
    public string NextRunText { get; }

    /// <summary>True when the last run failed or the workflow is paused.</summary>
    public bool NeedsLook { get; }

    /// <summary>True when another workflow has the same name: the short id shows beside it.</summary>
    public bool ShowsId { get; internal set; }

    /// <summary>The columns at the list's width: each cell binds its width and visibility to it.</summary>
    public ColumnPlan Columns { get; internal set { Set(ref field, value); OnChanged(nameof(StripShown)); } } = ColumnPlan.Wide;

    /// <summary>The strip's slots as drawn: the newest ones when the width allows fewer than 10.</summary>
    public IReadOnlyList<RunBarViewModel> StripShown => [.. Strip.Skip(Strip.Count - Columns.StripSlots)];

    /// <summary>The mark in front of the name: a failed run, paused, succeeded, cancelled or off.</summary>
    public string LeadGlyph => IsPaused ? "\u23f8" : LastRunToneWord switch { "error" => "\u2715", "ok" => "\u2713", _ => IsActive ? (LastRunText.StartsWith("Cancelled", StringComparison.Ordinal) ? "\u2298" : "\u25cf") : "\u25cb" };

    /// <summary>True when the mark is the failed one.</summary>
    public bool LeadIsError => !IsPaused && string.Equals(LastRunToneWord, "error", StringComparison.Ordinal);

    /// <summary>True when the mark is the paused one.</summary>
    public bool LeadIsPaused => IsPaused;

    /// <summary>True when the mark is a success.</summary>
    public bool LeadIsOk => !IsPaused && string.Equals(LastRunToneWord, "ok", StringComparison.Ordinal);

    /// <summary>True when the mark is the quiet one (off, cancelled, never ran).</summary>
    public bool LeadIsQuiet => !LeadIsError && !LeadIsPaused && !LeadIsOk;

    /// <summary>True when State reads Active.</summary>
    public bool StateIsOk => IsActive && !IsPaused;

    /// <summary>True when the group's header shows above this row.</summary>
    public bool HasGroupHeader => GroupHeader.Length > 0;

    /// <summary>The right-hand line of a group header: why the group is first, or how it is ordered; empty on a row that does not start a group.</summary>
    public string GroupHint => GroupHeader.StartsWith("Needs", StringComparison.Ordinal) ? "last run failed, or paused with a schedule" : HasGroupHeader ? "latest activity first" : string.Empty;

    /// <summary>The last 10 runs, oldest first, always 10 slots.</summary>
    public IReadOnlyList<RunBarViewModel> Strip { get; }

    /// <summary>"Needs a look \u00b7 3" or "Everything else \u00b7 11" on the first row of its group; empty on the others.</summary>
    public string GroupHeader { get; internal set { Set(ref field, value); OnChanged(nameof(HasGroupHeader)); OnChanged(nameof(GroupHint)); } } = string.Empty;

    /// <summary>When the workflow last did something: its last run, else its last change. The list sorts on it.</summary>
    internal DateTimeOffset Activity { get; }

    /// <summary>The name commands use.</summary>
    public string Slug { get; }

    /// <summary>The name for people.</summary>
    public string NameText { get; }

    /// <summary>True when there is a revision to say (from the second revision on).</summary>
    public bool HasRevisionText => RevisionText.Length > 0;

    /// <summary>The chip's word without its glyph.</summary>
    public string ChipWord { get; }

    /// <summary>The chip's glyph.</summary>
    public string ChipGlyph { get; }

    /// <summary>The chip's StateChipView tone: ok (active) or neutral.</summary>
    public string ChipToneWord { get; }

    /// <summary>"Revision 2" or "Revision 1 active, 3 newest"; empty for a first revision and for a workflow with no active revision.</summary>
    public string RevisionText { get; }

    /// <summary>"Changed by you, 5m ago" for the person's own actor, "Changed by owner, 5m ago" or "session:abc" as served.</summary>
    public string ChangedText { get; }

    /// <summary>The row's accessible name.</summary>
    public string Name { get; }

    /// <summary>True when triggers run a revision.</summary>
    public bool IsActive { get; }

    /// <summary>True when an edit made a revision newer than the one triggers run: Activate puts it live.</summary>
    public bool HasNewerRevision { get; }

#pragma warning disable CA1822, MA0041 // the view binds the error tone, which a workflow never has, so it is a property too
    /// <summary>Always false: a workflow's chip has no error tone.</summary>
    public bool IsError => false;
#pragma warning restore CA1822, MA0041

    private static (string Text, string Tone) LastRunOf(WorkflowRowView row, bool paused, TimeProvider clock)
    {
        if (paused)
        {
            return ("Paused", "attention");   // never drops: when the State column goes, this chip still says it
        }

        if (row.LastRun is not { } last)
        {
            return (string.Empty, "neutral");
        }

        var status = last.Status.ToLowerInvariant();
        var tone = status switch
        {
            "succeeded" => "ok",
            "failed" => "error",
            "running" or "waiting" or "queued" => "running",
            _ => "neutral",
        };
        var word = Labels.WfChip(status).Word;
        return (word + " " + Labels.AgoWords(clock.GetUtcNow() - last.At), tone);
    }

    private static string NextRunOf(WorkflowRowView row, bool paused, TimeProvider clock)
    {
        if (paused)
        {
            return "-";
        }

        if (row.NextRunAt is not { } next)
        {
            return string.Empty;
        }

        var (nowLocal, nextLocal) = (clock.GetLocalNow(), TimeZoneInfo.ConvertTime(next, clock.LocalTimeZone));
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        return nowLocal.Date == nextLocal.Date ? nextLocal.ToString("H:mm", culture) : nextLocal.ToString("ddd H:mm", culture);
    }

    private static List<RunBarViewModel> StripOf(WorkflowRowView row, TimeProvider clock)
    {
        const int Slots = 10;
        var bars = (row.Recent ?? []).Take(Slots).Reverse().Select(r => RunBarViewModel.Of(r, clock)).ToList();   // served newest first, drawn oldest first
        while (bars.Count < Slots)
        {
            bars.Insert(0, RunBarViewModel.Empty);
        }

        return bars;
    }
}
