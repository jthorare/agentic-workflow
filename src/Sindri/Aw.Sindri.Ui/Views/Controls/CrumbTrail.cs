using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Run;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// A link-style breadcrumb, "~ / src / tabs": every step but the last is an underlined link that raises <see cref="Navigated"/>, the last
/// is plain text. The Files view and the map share it, so the two look and act the same.
/// </summary>
internal sealed class CrumbTrail : UserControl
{
    /// <summary>The steps to draw.</summary>
    public static readonly StyledProperty<IReadOnlyList<Crumb>?> CrumbsProperty = AvaloniaProperty.Register<CrumbTrail, IReadOnlyList<Crumb>?>(nameof(Crumbs));

    private readonly StackPanel _row = new() { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };

    static CrumbTrail()
    {
        _ = CrumbsProperty.Changed.AddClassHandler<CrumbTrail>((trail, _) => trail.Draw());
    }

    /// <summary>Initializes a new instance of the <see cref="CrumbTrail"/> class.</summary>
    public CrumbTrail()
    {
        Content = _row;
    }

    /// <summary>Raised when a link step is pressed.</summary>
    internal event EventHandler<ValueEventArgs<Crumb>>? Navigated;

    /// <summary>Gets or sets the steps to draw.</summary>
    internal IReadOnlyList<Crumb>? Crumbs
    {
        get => GetValue(CrumbsProperty);
        set => SetValue(CrumbsProperty, value);
    }

    private void Draw()
    {
        _row.Children.Clear();
        foreach (var crumb in Crumbs ?? [])
        {
            if (crumb.HasSeparator)
            {
                var slash = new TextBlock { Text = "/", VerticalAlignment = VerticalAlignment.Center };
                slash.Classes.Add("body");
                slash.Classes.Add("subtle");
                AutomationProperties.SetAccessibilityView(slash, AccessibilityView.Raw);
                _row.Children.Add(slash);
            }

            _row.Children.Add(crumb.IsLink ? Link(crumb) : Plain(crumb));
        }
    }

    private static TextBlock Plain(Crumb crumb)
    {
        var text = new TextBlock { Text = crumb.Text, VerticalAlignment = VerticalAlignment.Center };
        text.Classes.Add("body-strong");
        AutomationProperties.SetName(text, crumb.Text);
        return text;
    }

    private Button Link(Crumb crumb)
    {
        var text = new TextBlock { Text = crumb.Text, TextDecorations = TextDecorations.Underline };
        text.Classes.Add("body-strong");
        var button = new Button { Content = text, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0), MinHeight = 0, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(button, crumb.Text);
        button.Click += (_, _) => Navigated?.Invoke(this, new ValueEventArgs<Crumb>(crumb));
        return button;
    }
}
