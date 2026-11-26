namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The open workflow's state beside its title on the runs level: the text and the header chip.</summary>
internal sealed partial class WorkflowsPageViewModel
{
    /// <summary>True when the header chip shows beside the open workflow's title (on its runs level): only for a workflow that is off (#3684 frames:
    /// cards.png "a paused workflow shows this chip in its header"; canvas-16.png, an active workflow's header has no chip).</summary>
    public bool ShowsDetailState => IsRuns && DetailActive == false;

    /// <summary>The open workflow's state, as its list row says it ("● Active", "○ Off", "○ Archived"): from the workflow read, else from the list row; empty when neither is known.</summary>
    public string DetailStateText => DetailActive is { } active ? StateOf(active, DetailArchived) : string.Empty;

    /// <summary>The word of the open workflow's state chip in the header ("Active", "Off", "Archived"); empty when no state is known.</summary>
    public string DetailChipWord => DetailActive is { } active ? Labels.WfState(active, DetailArchived).Word : string.Empty;

    /// <summary>The glyph of the header's state chip.</summary>
    public string DetailChipGlyph => DetailActive is { } active ? Labels.WfState(active, DetailArchived).Glyph : string.Empty;

    /// <summary>The tone of the header's state chip: "ok" when active, "neutral" when off.</summary>
    public string DetailChipTone => DetailActive is { } active ? (active ? "ok" : "neutral") : string.Empty;

    // An archived workflow counts as off: its chip says "Archived" in the neutral tone, never "Active" (#3655).
    private bool? DetailActive => Runs.Shown is { } shown
        ? shown.ActiveRevision is not null && shown.ArchivedAt is null
        : Rows.FirstOrDefault(r => string.Equals(r.Slug, Runs.Slug, StringComparison.Ordinal)) is { } row ? row.IsActive && !row.IsArchived : null;

    private bool DetailArchived => Runs.Shown is { } shown
        ? shown.ArchivedAt is not null
        : Rows.FirstOrDefault(r => string.Equals(r.Slug, Runs.Slug, StringComparison.Ordinal)) is { IsArchived: true };

    private static string StateOf(bool active, bool archived)
    {
        var (glyph, word) = Labels.WfState(active, archived);
        return glyph + " " + word;
    }
}
