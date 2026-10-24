namespace Aw.Sindri.Ui.ViewModels.Controls;

/// <summary>One step of a breadcrumb on the Context page, in the Files view and on the map alike: "~" for the top, then a level each. Every step but the last is a link.</summary>
/// <param name="Text">What the step says: "~" or the level's name.</param>
/// <param name="Path">The folder it goes to (as served, ending in a slash) in the Files view; null for the top and on the map.</param>
/// <param name="IsLink">Whether the step goes anywhere; the last step is where you are, plain text.</param>
/// <param name="IsTop">Whether the step is "~".</param>
internal sealed record Crumb(string Text, string? Path, bool IsLink, bool IsTop = false)
{
    /// <summary>The "~" step.</summary>
    /// <param name="isLink">Whether there is a level below it to come back from.</param>
    /// <returns>The step.</returns>
    internal static Crumb Top(bool isLink) => new(Labels.ContextFilesTop, Path: null, isLink, IsTop: true);

    /// <summary>Whether the step is where you are, plain text.</summary>
    public bool IsPlain => !IsLink;

    /// <summary>Whether a slash comes before the step: every step but "~".</summary>
    public bool HasSeparator => !IsTop;
}
