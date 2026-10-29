#pragma warning disable CA1822, MA0041, MA0182, S3237, IDE0060, CA1801, S1172 // red stubs: replaced by the implementation commit
using Aw.Sindri.Ui.Api.Projects;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>One project's card on the Projects page (mockup B). Kept by id across reads and updated in place; it maps served fields to words and decides nothing.</summary>
internal sealed class ProjectCardViewModel : ObservableObject
{
    internal ProjectCardViewModel(ProjectCardView card, TimeProvider clock)
    {
        throw new NotSupportedException();
    }

    /// <summary>The project's id.</summary>
    public string Id => throw new NotSupportedException();

    /// <summary>The version a write sends.</summary>
    public long Version => throw new NotSupportedException();

    /// <summary>The name.</summary>
    public string Name => throw new NotSupportedException();

    /// <summary>"attached", "by hand", or null for a led project.</summary>
    public string? OriginChip => throw new NotSupportedException();

    /// <summary>"on track", "at risk", "off track", or null.</summary>
    public string? HealthWord => throw new NotSupportedException();

    /// <summary>The health dot's tone: Ok, Attn or Fail; Neutral when there is no health.</summary>
    public ChipTone HealthTone => throw new NotSupportedException();

    /// <summary>The state in words.</summary>
    public string StateWord => throw new NotSupportedException();

    /// <summary>The open issue count, "…" while counting, null when unknown.</summary>
    public string? IssuesValue => throw new NotSupportedException();

    /// <summary>"open issues", or "counting".</summary>
    public string? IssuesLabel => throw new NotSupportedException();

    /// <summary>The open PR count, or null.</summary>
    public string? PrsValue => throw new NotSupportedException();

    /// <summary>The first line of the note, cut to 80 characters; null without a note.</summary>
    public string? NotesFace => throw new NotSupportedException();

    /// <summary>The footer's parts, in order.</summary>
    public IReadOnlyList<string> Footer => throw new NotSupportedException();

    /// <summary>True when the project is no longer synced.</summary>
    public bool IsGone => throw new NotSupportedException();

    /// <summary>What a gone card says.</summary>
    public string GoneText => throw new NotSupportedException();

    /// <summary>True while the card shows its name and origin chip only (Context is down).</summary>
    public bool NamesOnly { get; internal set; }

    /// <summary>The card's accessible name.</summary>
    public string AccessibleName => throw new NotSupportedException();

    /// <summary>Takes a newer serving of the same project.</summary>
    /// <param name="card">The card as served now.</param>
    internal void Update(ProjectCardView card) => throw new NotSupportedException();
}
