using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels.Run;
using Aw.Sindri.Ui.Views.Icons;

namespace Aw.Sindri.Ui.Views.Run;

/// <summary>
/// One card of the run canvas (F3 spec 3 §2, "Stage card"): a header (the state's glyph, the name, what it runs), a strip of step rows that
/// are real buttons, and a footer that says the state in a word and a glyph. Built in code because each state names its brushes by key.
/// The selected card or step gets the 2 px focus colour. Fixed height, from <see cref="RunCardSizes"/>.
/// </summary>
internal sealed class RunCardView : Border
{
    private const double RowHeight = RunCardSizes.StepRow;

    private readonly RunCardViewModel _card;
    private readonly List<(RunStepViewModel Step, Button Row)> _rows = [];
    private bool _built;

    /// <summary>Initializes a new instance of the <see cref="RunCardView"/> class.</summary>
    /// <param name="card">The card to draw.</param>
    internal RunCardView(RunCardViewModel card)
    {
        _card = card;
        Width = card.Width;
        Height = card.Height;
        ClipToBounds = true;
        BorderThickness = new Thickness(1);
        AutomationProperties.SetName(this, card.AutomationName);
        AutomationProperties.SetHelpText(this, card.HelpText);
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Control);
        AutomationProperties.SetControlTypeOverride(this, Avalonia.Automation.Peers.AutomationControlType.Group);
    }

    /// <summary>Raised when the card or one of its steps is clicked; carries the card's id or the step's key.</summary>
    internal event EventHandler<ValueEventArgs<string>>? Picked;

    /// <summary>Gets the card's header button.</summary>
    internal Button? Header { get; private set; }

    /// <summary>Gets the step buttons by step key.</summary>
    internal IReadOnlyDictionary<string, Button> StepButtons => _rows.ToDictionary(r => r.Step.Key, r => r.Row, StringComparer.Ordinal);

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_built)
        {
            _built = true;
            Build();
        }

        _card.PropertyChanged += OnCardChanged;
        Paint();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _card.PropertyChanged -= OnCardChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void Build()
    {
        Background = this.Brush("Brush.Surface1");
        CornerRadius = this.Corner("Radius.LG", 12);
        var root = new Grid { RowDefinitions = new RowDefinitions($"{RunCardSizes.Header.ToString(CultureInfo.InvariantCulture)},*,{RunCardSizes.Footer.ToString(CultureInfo.InvariantCulture)}") };
        Header = Flat(HeaderContent(), _card.AutomationName, () => Picked?.Invoke(this, new ValueEventArgs<string>(_card.Id)));
        Header.Height = RunCardSizes.Header;
        root.Children.Add(Header);
        if (_card.HasSteps)
        {
            var strip = new Border
            {
                Background = this.Brush("Brush.Surface2"),
                BorderBrush = this.Brush("Brush.Hairline"),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(10),
                Child = Steps(),
            };
            Grid.SetRow(strip, 1);
            root.Children.Add(strip);
        }

        var footer = Footer();
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        Child = root;
    }

    private Grid HeaderContent()
    {
        var tile = new Border
        {
            Width = 28,
            Height = 28,
            Background = this.Brush("Brush.Surface3"),
            CornerRadius = this.Corner("Radius.SM", 6),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new StateIcon { Icon = _card.Icon, Foreground = this.Brush(_card.TextBrush), Spins = _card.State == RunNodeState.Running, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        var words = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        words.Children.Add(RunResources.Text(_card.Title, 14, 19, FontWeight.SemiBold, this.Brush("Brush.Ink")));
        if (_card.Sub.Length > 0)
        {
            words.Children.Add(RunResources.Text(_card.Sub, 12, 16, FontWeight.Normal, this.Brush("Brush.InkSubtle")));
        }

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 10, Margin = new Thickness(14, 12) };
        grid.Children.Add(tile);
        Grid.SetColumn(words, 1);
        grid.Children.Add(words);
        return grid;
    }

    private StackPanel Steps()
    {
        var stack = new StackPanel();
        foreach (var step in _card.Steps)
        {
            var row = Flat(StepContent(step), step.AutomationName, () => Picked?.Invoke(this, new ValueEventArgs<string>(step.Key)));
            row.Height = RowHeight;
            row.Padding = new Thickness(6, 0);
            row.CornerRadius = this.Corner("Radius.SM", 6);
            stack.Children.Add(row);
            _rows.Add((step, row));
            step.PropertyChanged += (_, _) => Paint();
        }

        return stack;
    }

    private Grid StepContent(RunStepViewModel step)
    {
        var number = new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(8),
            BorderBrush = this.Brush("Brush.HairlineStrong"),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = step.Number.ToString(System.Globalization.CultureInfo.InvariantCulture), FontSize = 11, LineHeight = 14, FontWeight = FontWeight.SemiBold, Foreground = this.Brush("Brush.InkMuted"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        var name = RunResources.Text(step.Name, 12, 16, FontWeight.Medium, this.Brush("Brush.Ink"));
        name.VerticalAlignment = VerticalAlignment.Center;
        var state = new StateIcon { Icon = step.Icon, Foreground = this.Brush(step.TextBrush), Spins = step.State == RunNodeState.Running, VerticalAlignment = VerticalAlignment.Center };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 8 };
        grid.Children.Add(number);
        Grid.SetColumn(name, 1);
        grid.Children.Add(name);
        Grid.SetColumn(state, 2);
        grid.Children.Add(state);
        return grid;
    }

    private Border Footer()
    {
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        line.Children.Add(new StateIcon { Icon = _card.Icon, Foreground = this.Brush(_card.TextBrush), Spins = _card.State == RunNodeState.Running, VerticalAlignment = VerticalAlignment.Center });
        line.Children.Add(RunResources.Text(_card.FooterText, 11, 14, FontWeight.Medium, this.Brush(_card.TextBrush)));
        return new Border
        {
            Background = this.Brush(_card.SoftBrush),
            BorderBrush = this.Brush("Brush.Hairline"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(14, 0),
            Child = line,
        };
    }

    // A real button with no chrome: it is in the tab order and has a name, but looks like part of the card.
    private static Button Flat(Control content, string name, Action click)
    {
        var button = new Button
        {
            Content = content,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
        };
        AutomationProperties.SetName(button, name);
        button.Click += (_, _) => click();
        return button;
    }

    private void OnCardChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(RunCardViewModel.IsSelected), StringComparison.Ordinal))
        {
            Paint();
        }
    }

    // The ring of a selected card, and the fill of a selected step row.
    private void Paint()
    {
        BorderBrush = this.Brush(_card.IsSelected ? "Brush.PrimaryHover" : "Brush.Hairline");
        BorderThickness = new Thickness(_card.IsSelected ? 2 : 1);
        foreach (var (step, row) in _rows)
        {
            row.Background = step.IsSelected ? this.Brush("Brush.Surface3") : Brushes.Transparent;
        }
    }
}
