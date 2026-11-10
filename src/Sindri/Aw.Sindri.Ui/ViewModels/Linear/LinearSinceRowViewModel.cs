using Aw.Sindri.Ui.Api.Linear;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>One issue of the since-you-looked card: its title, everything that changed on it, when, and the chips the list already knows.</summary>
internal sealed class LinearSinceRowViewModel : ObservableObject
{
    internal LinearSinceRowViewModel(IReadOnlyList<LinearSinceItem> changes, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(changes);
        _ = now;
        Key = changes.Count > 0 ? changes[0].Key : string.Empty;
    }

    /// <summary>The issue key.</summary>
    public string Key { get; }

    /// <summary>The group the row sits in.</summary>
    public LinearSinceGroup Group { get; private set; }

    /// <summary>The newest change's instant.</summary>
    public DateTimeOffset At { get; private set; }

    /// <summary>The issue's title: the row's main line.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>The second line: the key, then each change, newest first.</summary>
    public string Detail { get; private set; } = string.Empty;

    /// <summary>"2h ago".</summary>
    public string TimeText { get; private set; } = string.Empty;

    /// <summary>"Todo → In Progress"; empty without a state change or a known state.</summary>
    public string StateChip { get; private set; } = string.Empty;

    /// <summary>"Urgent"; empty until the list knows the issue, and for no priority.</summary>
    public string PriorityChip { get; private set; } = string.Empty;

    /// <summary>The project's name; empty when the list doesn't know the issue or it has none.</summary>
    public string Project { get; private set; } = string.Empty;

    /// <summary>The assignee's initials; empty when the list doesn't know the issue or it is unassigned.</summary>
    public string Initials { get; private set; } = string.Empty;

    /// <summary>The row's accessible name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>True when there is a state chip.</summary>
    public bool HasStateChip { get; private set; }

    /// <summary>True when there is a priority chip.</summary>
    public bool HasPriorityChip { get; private set; }

    /// <summary>True when there is a project chip.</summary>
    public bool HasProject { get; private set; }

    /// <summary>True when there are initials.</summary>
    public bool HasInitials { get; private set; }

    /// <summary>Takes what the issue list knows of this issue (or nothing, when it isn't listed).</summary>
    /// <param name="issue">The listed issue.</param>
    internal void Apply(LinearIssueRow? issue) => HasProject = issue is null;   // stub: the red commit
}
