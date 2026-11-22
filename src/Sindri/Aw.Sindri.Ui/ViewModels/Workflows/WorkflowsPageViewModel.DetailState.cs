namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The open workflow's state beside its title on the runs level: the text and the header chip.</summary>
internal sealed partial class WorkflowsPageViewModel
{
    /// <summary>True when the header chip shows beside the open workflow's title (on its runs level): only for a workflow that is off (#3684 frames:
    /// cards.png "a paused workflow shows this chip in its header"; canvas-16.png, an active workflow's header has no chip).</summary>
    public bool ShowsDetailState => IsRuns && DetailActive == false;

    /// <summary>The open workflow's state, as its list row says it ("● Active", "○ Off"): from the workflow read, else from the list row; empty when neither is known.</summary>
    public string DetailStateText => Runs.Shown is { } shown
        ? StateOf(shown.ActiveRevision is not null)
        : Rows.FirstOrDefault(r => string.Equals(r.Slug, Runs.Slug, StringComparison.Ordinal)) is { } row ? StateOf(row.IsActive) : string.Empty;

    /// <summary>The word of the open workflow's state chip in the header ("Active", "Off"); empty when no state is known.</summary>
    public string DetailChipWord => DetailActive is { } active ? Labels.WfState(active).Word : string.Empty;

    /// <summary>The glyph of the header's state chip.</summary>
    public string DetailChipGlyph => DetailActive is { } active ? Labels.WfState(active).Glyph : string.Empty;

    /// <summary>The tone of the header's state chip: "ok" when active, "neutral" when off.</summary>
    public string DetailChipTone => DetailActive is { } active ? (active ? "ok" : "neutral") : string.Empty;

    private bool? DetailActive => Runs.Shown is { } shown
        ? shown.ActiveRevision is not null
        : Rows.FirstOrDefault(r => string.Equals(r.Slug, Runs.Slug, StringComparison.Ordinal)) is { } row ? row.IsActive : null;

    private static string StateOf(bool active)
    {
        var (glyph, word) = Labels.WfState(active);
        return glyph + " " + word;
    }
}
