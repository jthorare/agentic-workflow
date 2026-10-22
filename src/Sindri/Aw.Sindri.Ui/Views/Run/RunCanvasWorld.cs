using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels.Run;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Views.Run;

/// <summary>
/// The run canvas's world: the edges layer, the cards and the label pills, in that order from the bottom, placed where
/// <see cref="RunCanvasLayout"/> put them. The world's own coordinates run negative (the main path is centred on x = 0), so every child is
/// moved by <see cref="Origin"/> to keep the canvas's coordinates positive; the page converts between the two. The
/// <see cref="ZoomBorder"/> that holds it moves and scales it.
/// </summary>
internal sealed class RunCanvasWorld : Canvas
{
    /// <summary>The page to draw; null draws nothing.</summary>
    internal static readonly StyledProperty<RunPageViewModel?> PageProperty = AvaloniaProperty.Register<RunCanvasWorld, RunPageViewModel?>(nameof(Page));

    static RunCanvasWorld()
    {
        _ = PageProperty.Changed.AddClassHandler<RunCanvasWorld>((w, e) => w.Attach(e.OldValue as RunPageViewModel, e.NewValue as RunPageViewModel));
    }

    /// <summary>Raised when a card or one of its steps was clicked; carries the card's id or the step's key.</summary>
    internal event EventHandler<ValueEventArgs<string>>? Picked;

    /// <summary>Gets the point of the canvas where the world's origin sits.</summary>
    internal Point Origin { get; private set; }

    /// <summary>Gets the card views by card id.</summary>
    internal IReadOnlyDictionary<string, RunCardView> CardViews { get; private set; } = new Dictionary<string, RunCardView>(StringComparer.Ordinal);

    /// <summary>Gets the edges layer.</summary>
    internal GraphEdgesControl? EdgesLayer { get; private set; }

    /// <summary>Gets or sets the page to draw.</summary>
    internal RunPageViewModel? Page
    {
        get => GetValue(PageProperty);
        set => SetValue(PageProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Rebuild();   // the brushes and radii it reads come from the tree it is now in
    }

    private void Attach(RunPageViewModel? old, RunPageViewModel? page)
    {
        old?.PropertyChanged -= OnPageChanged;
        page?.PropertyChanged += OnPageChanged;
        Rebuild();
    }

    private void OnPageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(RunPageViewModel.Cards), StringComparison.Ordinal))
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        Children.Clear();
        CardViews = new Dictionary<string, RunCardView>(StringComparer.Ordinal);
        EdgesLayer = null;
        if (Page is not { Cards.Count: > 0 } page)
        {
            Origin = default;
            return;
        }

        var world = page.World;
        Origin = new Point(-world.X, -world.Y);
        Width = world.Width;
        Height = world.Height;
        EdgesLayer = new GraphEdgesControl { Width = world.Width, Height = world.Height, Edges = page.Edges, IsHitTestVisible = false };
        Place(EdgesLayer, Origin.X, Origin.Y);
        var views = new Dictionary<string, RunCardView>(StringComparer.Ordinal);
        foreach (var card in page.Cards)
        {
            var view = new RunCardView(card);
            view.Picked += (_, e) => Picked?.Invoke(this, e);
            views[card.Id] = view;
            Place(view, card.X + Origin.X, card.Y + Origin.Y);
        }

        CardViews = views;
        foreach (var pill in page.Pills)
        {
            Place(Pill(pill), pill.X + Origin.X, pill.Y + Origin.Y);
        }
    }

    private void Place(Control control, double x, double y)
    {
        SetLeft(control, x);
        SetTop(control, y);
        Children.Add(control);
    }

    // A pill is centred on its point: a zero-size host the pill overflows evenly on both sides.
    private Grid Pill(RunPillViewModel pill)
    {
        var text = RunResources.Text(pill.Text, 11, 16, FontWeight.Medium, this.Brush(pill.Failed ? "Brush.Error" : "Brush.InkMuted"));
        var box = new Border
        {
            Background = this.Brush("Brush.Surface3"),
            BorderBrush = this.Brush("Brush.Hairline"),
            BorderThickness = new Thickness(1),
            CornerRadius = this.Corner("Radius.SM", 6),
            Padding = new Thickness(6, 2),
            Child = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        };
        AutomationProperties.SetName(box, pill.Text);
        AutomationProperties.SetAccessibilityView(box, AccessibilityView.Control);
        AutomationProperties.SetControlTypeOverride(box, Avalonia.Automation.Peers.AutomationControlType.Text);
        return new Grid { Width = 0, Height = 0, Children = { box }, IsHitTestVisible = false };
    }
}
