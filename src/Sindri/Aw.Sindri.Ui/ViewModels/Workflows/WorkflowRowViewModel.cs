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
        StateText = glyph + " " + word;
        RevisionText = Labels.WfRevision(row.ActiveRevision, row.LatestRevision);
        ChangedText = Labels.WfChangedBy(row.ChangedBy, clock.GetUtcNow() - row.ChangedAt);
        Name = Labels.WfRowName(NameText, word.ToLowerInvariant(), RevisionText.ToLowerInvariant(), ChangedText.ToLowerInvariant());
    }

    /// <summary>The name commands use.</summary>
    public string Slug { get; }

    /// <summary>The name for people.</summary>
    public string NameText { get; }

    /// <summary>The state: glyph and word.</summary>
    public string StateText { get; }

    /// <summary>"Revision 1", "Revision 1 active, 3 newest" or "Newest revision 1".</summary>
    public string RevisionText { get; }

    /// <summary>"Changed by owner, 5m ago"; "session:abc" is shown as served.</summary>
    public string ChangedText { get; }

    /// <summary>The row's accessible name.</summary>
    public string Name { get; }

    /// <summary>True when triggers run a revision.</summary>
    public bool IsActive { get; }

    /// <summary>True when an edit made a revision newer than the one triggers run: Activate puts it live.</summary>
    public bool HasNewerRevision { get; }

    /// <summary>True when the workflow is on (the ok colour); the glyph and word say it too.</summary>
    public bool IsOk => IsActive;

#pragma warning disable CA1822, MA0041 // the view binds all three tones, so the two a workflow never has are properties too
    /// <summary>Always false: a workflow's chip has no attention tone.</summary>
    public bool IsAttention => false;

    /// <summary>Always false: a workflow's chip has no error tone.</summary>
    public bool IsError => false;
#pragma warning restore CA1822, MA0041
}
