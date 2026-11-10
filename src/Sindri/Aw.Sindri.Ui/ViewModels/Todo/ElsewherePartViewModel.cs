namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>One drawn part of the From elsewhere line: its words, where it goes, and (when its read failed) a Retry button.</summary>
/// <param name="Part">Which part.</param>
/// <param name="Text">The words: "2 PRs waiting on your review".</param>
/// <param name="Name">The name for a screen reader: "Opens Reviews: 2 PRs waiting on your review".</param>
/// <param name="CanRetry">True while the part says its read failed; the line draws Retry beside it.</param>
/// <param name="RetryName">The Retry button's name: "Retry Reviews"; empty when there is no button.</param>
internal sealed record ElsewherePartViewModel(ElsewherePart Part, string Text, string Name, bool CanRetry, string RetryName)
{
#pragma warning disable CA1822, MA0041 // red stub: the real value is true for the one part that asks the person to connect
    /// <summary>True when the part is an action (Connect Linear), drawn as a link in the primary colour.</summary>
    public bool IsAction => false;
#pragma warning restore CA1822, MA0041

    /// <summary>The Retry button's word.</summary>
    public string RetryText { get; } = Labels.ElsewhereRetry;
}
