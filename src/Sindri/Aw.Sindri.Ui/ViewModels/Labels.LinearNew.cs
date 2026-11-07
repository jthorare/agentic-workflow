namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Linear "New issue" dialog (Linear hub spec §3).</summary>
internal static partial class Labels
{
    /// <summary>The Team field's label.</summary>
    internal const string LinearNewTeam = "Team";

    /// <summary>The Title field's label.</summary>
    internal const string LinearNewTitle = "Title";

    /// <summary>The Description field's label.</summary>
    internal const string LinearNewDescription = "Description";

    /// <summary>The State field's label.</summary>
    internal const string LinearNewState = "State";

    /// <summary>The Assignee field's label.</summary>
    internal const string LinearNewAssignee = "Assignee";

    /// <summary>The Priority field's label.</summary>
    internal const string LinearNewPriority = "Priority";

    /// <summary>The Labels field's label.</summary>
    internal const string LinearNewLabels = "Labels";

    /// <summary>The Cycle field's label.</summary>
    internal const string LinearNewCycle = "Cycle";

    /// <summary>The state picker's clear entry: no state is sent and Linear picks the team's default.</summary>
    internal const string LinearNewDefaultState = "Team default";

    /// <summary>The Create button.</summary>
    internal const string LinearNewCreate = "Create issue";

    /// <summary>The Cancel button.</summary>
    internal const string LinearNewCancel = "Cancel";

    /// <summary>The line under the Title field after a blank submit.</summary>
    internal const string LinearNewTitleRequired = "Title is required";

    /// <summary>The line under the Team field after a submit with no team.</summary>
    internal const string LinearNewTeamRequired = "Team is required";

    /// <summary>The line over a reopened dialog that still holds the last draft.</summary>
    internal const string LinearNewResumeDraft = "Resume draft";

    /// <summary>The button that throws the draft away.</summary>
    internal const string LinearNewDiscardDraft = "Discard draft";

    /// <summary>The line after a create with no answer; Retry sits beside it.</summary>
    internal const string LinearNewUnknown = "Linear didn't confirm the issue. Retry?";

    /// <summary>The note over pickers that have nothing to offer for a team that has not synced.</summary>
    /// <param name="team">The team key.</param>
    /// <returns>"ENG's states haven't synced yet. They arrive with the next sync (within 5 min)".</returns>
    internal static string LinearNewNotSynced(string team) => $"{team}'s states haven't synced yet. They arrive with the next sync (within 5 min)";

    /// <summary>The announcement after a create.</summary>
    /// <param name="key">The new issue's key.</param>
    /// <returns>"Created ENG-58".</returns>
    internal static string LinearNewCreated(string key) => $"Created {key}";
}
