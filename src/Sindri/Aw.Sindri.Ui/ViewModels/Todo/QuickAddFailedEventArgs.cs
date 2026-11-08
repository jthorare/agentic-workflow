namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>What the quick-add box could not save after it had closed.</summary>
/// <param name="text">The todo's text, without its day word.</param>
/// <param name="reason">Why Sindri refused, as the box shows it.</param>
internal sealed class QuickAddFailedEventArgs(string text, string reason) : EventArgs
{
    /// <summary>The todo's text.</summary>
    internal string Text { get; } = text;

    /// <summary>Why the add failed.</summary>
    internal string Reason { get; } = reason;
}
