using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Aw.Sindri.Ui.ViewModels.Todo;

namespace Aw.Sindri.Ui.Views.Todo;

/// <summary>
/// The Todo panel's From elsewhere line (todo spec §3.1, §3.4, §3.5): it draws the view model's parts as links and calls its methods; it decides nothing.
/// One focus stop: Left and Right walk the parts (a failed part's Retry follows it) and Enter presses the one in front.
/// </summary>
internal sealed partial class FromElsewhereLine : UserControl
{
    private readonly List<(Button Button, Action Press)> _stops = [];
    private FromElsewhereViewModel? _line;
    private int _active;
    private bool _attached;

    public FromElsewhereLine()
    {
        InitializeComponent();
        ElsewhereFrame.GotFocus += (_, _) => Mark();
        ElsewhereFrame.LostFocus += (_, _) => Mark();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Listen(on: false);
        _line = DataContext as FromElsewhereViewModel;
        Listen(on: _attached);
        Rebuild();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        Listen(on: true);
        Rebuild();   // the line may have changed while it was away
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        Listen(on: false);
        base.OnDetachedFromVisualTree(e);
    }

    // The view model outlives the view (the shell owns it), so the view follows it only while it is in the tree. Off first, then on: never twice.
    private void Listen(bool on)
    {
        if (_line is null)
        {
            return;
        }

        _line.PropertyChanged -= OnLineChanged;
        if (on)
        {
            _line.PropertyChanged += OnLineChanged;
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyModifiers != KeyModifiers.None || _stops.Count == 0 || !ElsewhereFrame.IsFocused)
        {
            return;
        }

        // X, Delete and Space are not taken
        if (e.Key is Key.Left or Key.Right)
        {
            _active = Math.Clamp(_active + (e.Key == Key.Right ? 1 : -1), 0, _stops.Count - 1);
            Mark();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            _stops[_active].Press();
            e.Handled = true;
        }
    }

    private void OnLineChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(FromElsewhereViewModel.Parts), StringComparison.Ordinal) || string.Equals(e.PropertyName, nameof(FromElsewhereViewModel.Text), StringComparison.Ordinal))
        {
            Rebuild();
        }
    }

    // The words again from the view model's parts: a link per part, Retry after a failed one, a dot between parts.
    private void Rebuild()
    {
        ElsewhereWords.Children.Clear();
        _stops.Clear();
        if (_line is null)
        {
            return;
        }

        if (_line.Parts.Count == 0)
        {
            ElsewhereWords.Children.Add(Words(_line.Text, tag: "ElsewhereText"));   // checking, or nothing waiting
            return;
        }

        foreach (var part in _line.Parts)
        {
            if (ElsewhereWords.Children.Count > 0)
            {
                ElsewhereWords.Children.Add(Words(" · ", tag: "ElsewhereDot", visible: false));
            }

            var line = _line;
            ElsewhereWords.Children.Add(Link(part.Text, part.Name, "Elsewhere" + part.Part, () => line.Open(part.Part)));
            if (part.CanRetry)
            {
                ElsewhereWords.Children.Add(Link(part.RetryText, part.RetryName, "Elsewhere" + part.Part + "Retry", () => _ = line.Retry(part.Part)));
            }
        }

        _active = Math.Clamp(_active, 0, _stops.Count - 1);
        Mark();
    }

    private Button Link(string text, string name, string id, Action press)
    {
        var button = new Button { Name = id, Content = Words(text, tag: null), Classes = { "copy", "elsewhere-part" } };
        AutomationProperties.SetName(button, name);
        button.Click += (_, _) => press();
        _stops.Add((button, press));
        return button;
    }

    private static TextBlock Words(string text, string? tag, bool visible = true)
    {
        var block = new TextBlock { Text = text, Classes = { "caption" }, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Name = tag };
        if (!visible)
        {
            AutomationProperties.SetAccessibilityView(block, AccessibilityView.Raw);
        }
        else
        {
            AutomationProperties.SetName(block, text);
        }

        return block;
    }

    // The part in front is underlined while the line has focus.
    private void Mark()
    {
        for (var i = 0; i < _stops.Count; i++)
        {
            _stops[i].Button.Classes.Set("active", ElsewhereFrame.IsFocused && i == _active);
        }
    }
}
