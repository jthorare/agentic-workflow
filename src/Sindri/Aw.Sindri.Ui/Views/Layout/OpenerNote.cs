using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Aw.Sindri.Ui.Views.Layout;

/// <summary>
/// A page's button tells the shell window it is about to open an overlay, so closing the overlay gives focus back to that button. It is a routed event that bubbles from the page
/// to the window, so no page needs to know the window.
/// </summary>
internal sealed class OpenerNote : Interactive
{
    /// <summary>Raised by a page's button just before it opens an overlay.</summary>
    public static readonly RoutedEvent<OpenerNoteEventArgs> NotedEvent =
        RoutedEvent.Register<OpenerNote, OpenerNoteEventArgs>("Noted", RoutingStrategies.Bubble);

    /// <summary>Raises <see cref="NotedEvent"/> from <paramref name="page"/> for a pressed button.</summary>
    /// <param name="page">The page (or any control in it) whose button was pressed.</param>
    /// <param name="sender">The button.</param>
    internal static void Raise(Control page, object? sender)
    {
        if (sender is Control button)
        {
            page.RaiseEvent(new OpenerNoteEventArgs(button) { RoutedEvent = NotedEvent, Source = page });
        }
    }
}
