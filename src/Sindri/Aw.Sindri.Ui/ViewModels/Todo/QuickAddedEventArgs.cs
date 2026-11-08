using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>What the quick-add box saved.</summary>
/// <param name="saved">Sindri's answer: the new todo's id and version.</param>
/// <param name="due">The day the text named, or null.</param>
internal sealed class QuickAddedEventArgs(TodoAcceptedView saved, DateOnly? due) : EventArgs
{
    /// <summary>Sindri's answer.</summary>
    internal TodoAcceptedView Saved { get; } = saved;

    /// <summary>The day the text named, or null.</summary>
    internal DateOnly? Due { get; } = due;
}
