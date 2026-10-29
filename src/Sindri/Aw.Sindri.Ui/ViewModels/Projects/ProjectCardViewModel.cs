using System.Globalization;
using Aw.Sindri.Ui.Api.Projects;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>One project's card on the Projects page (mockup B). Kept by id across reads and updated in place; it maps served fields to words and decides nothing.</summary>
internal sealed class ProjectCardViewModel : ObservableObject
{
    private readonly TimeProvider _clock;

    internal ProjectCardViewModel(ProjectCardView card, TimeProvider clock)
    {
        (Card, _clock) = (card, clock);
        Derive();
    }

    /// <summary>The project as last served.</summary>
    internal ProjectCardView Card { get; private set; }

    /// <summary>The project's id.</summary>
    public string Id => Card.Id;

    /// <summary>The version a write sends.</summary>
    public long Version => Card.Version;

    /// <summary>The name.</summary>
    public string Name => Card.Name;

    /// <summary>"attached", "by hand", or null for a led project.</summary>
    public string? OriginChip { get; private set => Set(ref field, value); }

    /// <summary>"on track", "at risk", "off track", or null.</summary>
    public string? HealthWord { get; private set => Set(ref field, value); }

    /// <summary>The health dot's tone: Ok, Attn or Fail; Neutral when there is no health.</summary>
    public ChipTone HealthTone { get; private set => Set(ref field, value); } = ChipTone.Neutral;

    /// <summary>The state in words.</summary>
    public string StateWord { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The open issue count, "…" while counting, null when unknown.</summary>
    public string? IssuesValue { get; private set => Set(ref field, value); }

    /// <summary>"open issues", or "counting"; null with no count.</summary>
    public string? IssuesLabel { get; private set => Set(ref field, value); }

    /// <summary>The open PR count, or null.</summary>
    public string? PrsValue { get; private set => Set(ref field, value); }

    /// <summary>"open PRs", the label under the PR count.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string PrsLabel => Labels.ProjectPrsLabel;
#pragma warning restore CA1822, MA0041

    /// <summary>The first line of the note, cut to 80 characters; null without a note.</summary>
    public string? NotesFace { get; private set => Set(ref field, value); }

    /// <summary>The footer's parts, in order.</summary>
    public IReadOnlyList<string> Footer { get; private set => Set(ref field, value); } = [];

    /// <summary>The footer's parts joined with " · ".</summary>
    public string FooterText => string.Join(" · ", Footer);

    /// <summary>True when the project is no longer synced.</summary>
    public bool IsGone { get; private set => Set(ref field, value); }

    /// <summary>What a gone card says.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string GoneText => Labels.ProjectGone;
#pragma warning restore CA1822, MA0041

    /// <summary>True while the card shows its name and origin chip only (Context is down).</summary>
    public bool NamesOnly { get; internal set => Set(ref field, value); }

    /// <summary>The card's accessible name.</summary>
    public string AccessibleName { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Takes a newer serving of the same project and redraws only what changed.</summary>
    /// <param name="card">The card as served now.</param>
    internal void Update(ProjectCardView card)
    {
        Card = card;
        Derive();
    }

    private void Derive()
    {
        OnChanged(nameof(Name));
        OnChanged(nameof(Version));
        OriginChip = Labels.ProjectOriginChip(Card.Origin);
        HealthWord = Labels.ProjectHealthWord(Card.Health);
        HealthTone = HealthWord is null ? ChipTone.Neutral : Card.Health switch
        {
            "onTrack" => ChipTone.Ok,
            "atRisk" => ChipTone.Attn,
            _ => ChipTone.Fail,
        };
        StateWord = Labels.ProjectStateWord(Card.State);
        (IssuesValue, IssuesLabel) = Card.Counting
            ? ("…", "counting")
            : Card.OpenIssues is { } issues ? (issues.ToString(CultureInfo.InvariantCulture), "open issues") : (null, null);
        PrsValue = Card.OpenPrs?.ToString(CultureInfo.InvariantCulture);
        NotesFace = Face(Card.Note);
        IsGone = !Card.Synced;
        Footer = Parts();
        OnChanged(nameof(FooterText));
        AccessibleName = Spoken();
    }

    private static string? Face(string? note)
    {
        var line = note?.Split('\n')[0].Trim();
        if (string.IsNullOrEmpty(line))
        {
            return null;
        }

        return line.Length <= Labels.ProjectNoteFaceMax ? line : line[..(Labels.ProjectNoteFaceMax - 1)].TrimEnd() + "…";
    }

    private List<string> Parts()
    {
        var parts = new List<string>();
        var finished = Card.FinishedAt is not null;
        if (Card.FinishedAt is { } at)
        {
            parts.Add(Labels.ProjectFinishedOn(Card.State, at));
        }

        if (Card.Lead is { } lead)
        {
            parts.Add(Labels.ProjectLead(lead.You, lead.Name));
        }

        if (!finished && Card.Target is { } target)
        {
            parts.Add(Labels.ProjectTarget(target));
        }

        if (Card.Repos.Count > 0)
        {
            parts.Add(string.Join(", ", Card.Repos));
        }

        if (Card.LastActivity is { } activity)
        {
            parts.Add(Labels.ProjectActive(_clock.GetUtcNow() - activity));
        }

        return parts;
    }

    private string Spoken()
    {
        var parts = new List<string> { Card.Name, StateWord };
        if (HealthWord is not null)
        {
            parts.Add(HealthWord);
        }

        if (!Card.Counting && Card.OpenIssues is { } issues)
        {
            parts.Add(Labels.ProjectOpenIssues(issues));
        }

        if (Card.OpenPrs is { } prs)
        {
            parts.Add(Labels.ProjectOpenPrs(prs));
        }

        return string.Join(", ", parts);
    }
}
