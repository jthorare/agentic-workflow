using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Aw.Sindri.Ui.Views.Layout;

/// <summary>The button that is about to open an overlay.</summary>
/// <param name="invoker">The button.</param>
internal sealed class OpenerNoteEventArgs(Control invoker) : RoutedEventArgs
{
    /// <summary>Gets the button.</summary>
    public Control Invoker { get; } = invoker;
}
