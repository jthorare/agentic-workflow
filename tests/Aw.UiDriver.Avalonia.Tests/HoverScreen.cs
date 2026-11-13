using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace Aw.UiDriver.Avalonia.Tests;

/// <summary>
/// A window with one card that draws its state from <see cref="InputElement.IsPointerOver"/>, not from a style: its label reads "hovered" while
/// the pointer is over it and "idle" otherwise, and it counts the pointer entering and leaving. A visual layer manager gives it the overlay
/// layer a themed window's template has.
/// </summary>
internal sealed class HoverScreen
{
    internal HoverScreen()
    {
        Label = new TextBlock { Text = "idle" };
        AutomationProperties.SetAutomationId(Label, "card-label");
        Card = new Border { Width = 120, Height = 40, Background = Brushes.Gray, Child = Label };
        AutomationProperties.SetAutomationId(Card, "card");
        Card.PointerEntered += (_, _) => Entered++;
        Card.PointerExited += (_, _) => Exited++;
        Card.PropertyChanged += (_, e) =>
        {
            if (e.Property == InputElement.IsPointerOverProperty)
            {
                Label.Text = Card.IsPointerOver ? "hovered" : "idle";
            }
        };
        var form = new StackPanel { Margin = new Thickness(20), Children = { Card } };
        Window = new Window { Width = 400, Height = 300, Title = "Hover screen", Content = new VisualLayerManager { Child = form } };
        Window.Show();
    }

    internal Window Window { get; }

    internal Border Card { get; }

    internal TextBlock Label { get; }

    internal int Entered { get; private set; }

    internal int Exited { get; private set; }

    /// <summary>Builds the screen on the UI thread (kept running by <see cref="UiPump"/>).</summary>
    /// <returns>The screen.</returns>
    internal static Task<HoverScreen> OpenAsync() =>
        Dispatcher.UIThread.InvokeAsync(static () => new HoverScreen(), DispatcherPriority.Normal, TestContext.Current.CancellationToken).GetTask();
}
