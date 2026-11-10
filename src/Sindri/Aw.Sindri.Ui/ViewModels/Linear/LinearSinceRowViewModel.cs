using Aw.Sindri.Ui.Api.Linear;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>
/// One issue of the since-you-looked card: its title as the main line, everything that changed on it (newest first) as the second line, when, and the chips.
/// The state chip comes from the state change itself; priority, project and assignee come from the issue list once it holds the issue, so a row never prints a blank chip.
/// </summary>
internal sealed class LinearSinceRowViewModel : ObservableObject
{
    private static readonly string[] _issueBound = [nameof(StateChip), nameof(HasStateChip), nameof(PriorityChip), nameof(HasPriorityChip), nameof(Project), nameof(HasProject), nameof(Initials), nameof(HasInitials), nameof(IsUrgent)];

    private readonly string? _movedChip;   // from the state change; null without one

    /// <summary>Builds the row of one issue.</summary>
    /// <param name="changes">Every change on the issue; at least one.</param>
    /// <param name="now">The instant "ago" counts from.</param>
    internal LinearSinceRowViewModel(IReadOnlyList<LinearSinceItem> changes, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var newestFirst = changes.OrderByDescending(static c => c.At).ToList();
        var latest = newestFirst[0];
        Key = latest.Key;
        At = latest.At;
        Title = string.IsNullOrWhiteSpace(latest.Title) ? latest.Key : latest.Title;
        Group = changes.Any(static c => string.Equals(c.Kind, "assigned", StringComparison.Ordinal)) ? LinearSinceGroup.Assigned
            : changes.Any(static c => string.Equals(c.Kind, "state", StringComparison.Ordinal)) ? LinearSinceGroup.Moved
            : LinearSinceGroup.Other;
        var said = newestFirst.Select(Labels.LinearSinceChange).ToList();
        Detail = Labels.LinearSinceDetail(Key, newestFirst.Where(c => !IsShownElsewhere(c)).Select(Labels.LinearSinceChange));
        TimeText = Labels.AgoWords(now - At);
        Name = Key + ", " + Title + ", " + string.Join(", ", said) + ", " + TimeText;
        var moved = newestFirst.FirstOrDefault(static c => string.Equals(c.Kind, "state", StringComparison.Ordinal));
        _movedChip = moved is null ? null : Labels.LinearStateChip(moved.From, moved.To);
        Apply(issue: null);
    }

    /// <summary>The issue key.</summary>
    public string Key { get; }

    /// <summary>The group the row sits in.</summary>
    public LinearSinceGroup Group { get; }

    /// <summary>The newest change's instant.</summary>
    public DateTimeOffset At { get; }

    /// <summary>The issue's title: the row's main line.</summary>
    public string Title { get; }

    /// <summary>The second line: the key, then only the changes the chips and the group don't show (a mention), newest first.</summary>
    public string Detail { get; }

    /// <summary>"2h ago".</summary>
    public string TimeText { get; }

    /// <summary>The row's accessible name.</summary>
    public string Name { get; }

    /// <summary>"Todo → In Progress", or the issue's state from the list when the row has no state change; empty when neither is known.</summary>
    public string StateChip { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"Urgent"; empty until the list holds the issue, and for no priority.</summary>
    public string PriorityChip { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The project's name; empty until the list holds the issue, and when it has none.</summary>
    public string Project { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The assignee's initials; empty until the list holds the issue, and when it is unassigned.</summary>
    public string Initials { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the issue's priority is Urgent: the chip takes the attention colour.</summary>
    public bool IsUrgent { get; private set => Set(ref field, value); }

    /// <summary>True when there is a state chip.</summary>
    public bool HasStateChip => StateChip.Length > 0;

    /// <summary>True when there is a priority chip.</summary>
    public bool HasPriorityChip => PriorityChip.Length > 0;

    /// <summary>True when there is a project chip.</summary>
    public bool HasProject => Project.Length > 0;

    /// <summary>True when the avatar shows: the assignee is known, and the row is not in "Assigned to you" (there it is always the viewer).</summary>
    public bool HasInitials => Initials.Length > 0 && Group != LinearSinceGroup.Assigned;

    /// <summary>Takes what the issue list knows of this issue (or nothing, when it isn't listed).</summary>
    /// <param name="issue">The listed issue.</param>
    internal void Apply(LinearIssueRow? issue)
    {
        StateChip = _movedChip ?? issue?.State.Name ?? string.Empty;
        PriorityChip = PriorityOf(issue);
        Project = issue?.Project?.Name ?? string.Empty;
        Initials = InitialsOf(issue);
        IsUrgent = issue is { Priority: 1 };
        foreach (var name in _issueBound)
        {
            OnChanged(name);
        }
    }

    // A state move is the state chip; an assignment is the group's heading.
    private static bool IsShownElsewhere(LinearSinceItem change) => string.Equals(change.Kind, "state", StringComparison.Ordinal) || string.Equals(change.Kind, "assigned", StringComparison.Ordinal);

    private static string PriorityOf(LinearIssueRow? issue) => issue is { Priority: > 0 } ? Labels.LinearPriority(issue.Priority) : string.Empty;

    private static string InitialsOf(LinearIssueRow? issue) => issue?.Assignee is { } who ? Labels.LinearInitials(who.Name) : string.Empty;
}
