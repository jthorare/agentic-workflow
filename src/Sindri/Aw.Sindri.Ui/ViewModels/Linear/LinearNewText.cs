namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>The "New issue" dialog's own copy (<see cref="Labels"/>) as the view binds it.</summary>
internal sealed class LinearNewText
{
    /// <summary>The one instance.</summary>
    internal static LinearNewText Instance { get; } = new();

    /// <summary>The dialog's name and heading.</summary>
    public string Heading { get; } = Labels.LinearNewIssue;

    /// <summary><see cref="Labels.LinearNewTeam"/>.</summary>
    public string Team { get; } = Labels.LinearNewTeam;

    /// <summary><see cref="Labels.LinearNewTitle"/>.</summary>
    public string Title { get; } = Labels.LinearNewTitle;

    /// <summary><see cref="Labels.LinearNewDescription"/>.</summary>
    public string Description { get; } = Labels.LinearNewDescription;

    /// <summary><see cref="Labels.LinearNewState"/>.</summary>
    public string State { get; } = Labels.LinearNewState;

    /// <summary><see cref="Labels.LinearNewAssignee"/>.</summary>
    public string Assignee { get; } = Labels.LinearNewAssignee;

    /// <summary><see cref="Labels.LinearNewPriority"/>.</summary>
    public string Priority { get; } = Labels.LinearNewPriority;

    /// <summary><see cref="Labels.LinearNewLabels"/>.</summary>
    public string TickLabels { get; } = Labels.LinearNewLabels;

    /// <summary><see cref="Labels.LinearNewCycle"/>.</summary>
    public string Cycle { get; } = Labels.LinearNewCycle;

    /// <summary><see cref="Labels.LinearNewCancel"/>.</summary>
    public string Cancel { get; } = Labels.LinearNewCancel;

    /// <summary><see cref="Labels.LinearNewResumeDraft"/>.</summary>
    public string ResumeDraft { get; } = Labels.LinearNewResumeDraft;

    /// <summary><see cref="Labels.LinearNewDiscardDraft"/>.</summary>
    public string DiscardDraft { get; } = Labels.LinearNewDiscardDraft;

    /// <summary><see cref="Labels.LinearRetry"/>.</summary>
    public string Retry { get; } = Labels.LinearRetry;
}
