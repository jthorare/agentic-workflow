namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>The Projects page asks the view to move focus.</summary>
/// <param name="target">Where.</param>
/// <param name="cardId">The card's id when <paramref name="target"/> is a card.</param>
internal sealed class ProjectFocusEventArgs(ProjectFocus target, string? cardId) : EventArgs
{
    /// <summary>Where.</summary>
    public ProjectFocus Target { get; } = target;

    /// <summary>The card's id for <see cref="ProjectFocus.Card"/>.</summary>
    public string? CardId { get; } = cardId;
}
