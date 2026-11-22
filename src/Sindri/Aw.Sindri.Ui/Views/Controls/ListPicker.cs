using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.Views.Icons;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// The list picker (#3234): a heading, grouped selectable rows, an error line with its code under Details, and a Cancel button. It is the content of the shell's
/// sheet, so first focus and the focus trap come from that host; the picker gives the host <see cref="FocusFirst"/>. Up and Down move between the rows
/// across groups (a disabled row takes focus too, so its reason can be reached, but it is never picked), Home and End jump, Enter picks, Esc closes. The block picker and the Connect target list of the Workflows editor are both this part.
/// </summary>
[SuppressMessage("Design", "MA0182", Justification = "The Workflows editor adopts it in its own PR (#3234).")]
internal sealed class ListPicker : UserControl
{
    /// <summary>The heading.</summary>
    public static readonly StyledProperty<string?> HeadingProperty = AvaloniaProperty.Register<ListPicker, string?>(nameof(Heading));

    /// <summary>The groups of choices.</summary>
    public static readonly StyledProperty<IReadOnlyList<PickerGroup>?> GroupsProperty = AvaloniaProperty.Register<ListPicker, IReadOnlyList<PickerGroup>?>(nameof(Groups));

    /// <summary>The id of the chosen row, drawn selected; null for none.</summary>
    public static readonly StyledProperty<string?> SelectedIdProperty = AvaloniaProperty.Register<ListPicker, string?>(nameof(SelectedId));

    /// <summary>The error sentence; empty hides the line.</summary>
    public static readonly StyledProperty<string?> ErrorTextProperty = AvaloniaProperty.Register<ListPicker, string?>(nameof(ErrorText));

    /// <summary>The error's code, shown under Details.</summary>
    public static readonly StyledProperty<string?> ErrorCodeProperty = AvaloniaProperty.Register<ListPicker, string?>(nameof(ErrorCode));

    /// <summary>The Cancel button's label.</summary>
    public static readonly StyledProperty<string?> CancelTextProperty = AvaloniaProperty.Register<ListPicker, string?>(nameof(CancelText));

    private readonly TextBlock _heading = new();
    private readonly StackPanel _rows = new() { Spacing = 4 };
    private readonly Border _error = new() { IsVisible = false };
    private readonly TextBlock _errorText = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Expander _details = new() { Header = "Details", IsExpanded = false, IsVisible = false };
    private readonly TextBlock _code = new();
    private readonly Button _cancel = new() { HorizontalAlignment = HorizontalAlignment.Left };
    private readonly List<(SelectableTile Tile, PickerItem Item)> _tiles = [];

    static ListPicker()
    {
        _ = HeadingProperty.Changed.AddClassHandler<ListPicker>((p, _) => p.ShowHeading());
        _ = GroupsProperty.Changed.AddClassHandler<ListPicker>((p, _) => p.Rebuild());
        _ = SelectedIdProperty.Changed.AddClassHandler<ListPicker>((p, _) => p.MarkSelected());
        _ = ErrorTextProperty.Changed.AddClassHandler<ListPicker>((p, _) => p.ShowError());
        _ = ErrorCodeProperty.Changed.AddClassHandler<ListPicker>((p, _) => p.ShowError());
        _ = CancelTextProperty.Changed.AddClassHandler<ListPicker>((p, _) => p.ShowCancel());
    }

    /// <summary>Initializes a new instance of the <see cref="ListPicker"/> class.</summary>
    public ListPicker()
    {
        _heading.Classes.Add("body-strong");
        _heading.TextWrapping = TextWrapping.Wrap;
        _error.Classes.Add("picker-error");
        _error.Margin = new Thickness(0);
        _errorText.Classes.Add("body");
        AutomationProperties.SetAccessibilityView(_errorText, AccessibilityView.Raw);
        var icon = new StateIcon { Glyph = StateGlyph.Down, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 3, 0, 0) };
        icon.Classes.Add("error");
        _code.Classes.Add("caption");
        _code.Classes.Add("mono");
        _details.Content = _code;
        var lines = new StackPanel { Spacing = 4 };
        lines.Children.Add(_errorText);
        lines.Children.Add(_details);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(icon);
        row.Children.Add(lines);
        _error.Child = row;
        AutomationProperties.SetControlTypeOverride(_error, AutomationControlType.Text);
        _cancel.Click += (_, _) => Close();

        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(_heading);
        stack.Children.Add(_rows);
        stack.Children.Add(_error);
        stack.Children.Add(_cancel);
        Content = stack;
        AddHandler(KeyDownEvent, OnKeys, RoutingStrategies.Bubble);
    }

    /// <summary>Raised when a row is picked (Enter, Space or a click).</summary>
    internal event EventHandler<ValueEventArgs<PickerItem>>? Picked;

    /// <summary>Raised when Esc or Cancel closes the picker.</summary>
    internal event EventHandler? Closed;

    /// <summary>Gets or sets the heading.</summary>
    internal string? Heading
    {
        get => GetValue(HeadingProperty);
        set => SetValue(HeadingProperty, value);
    }

    /// <summary>Gets or sets the groups.</summary>
    internal IReadOnlyList<PickerGroup>? Groups
    {
        get => GetValue(GroupsProperty);
        set => SetValue(GroupsProperty, value);
    }

    /// <summary>Gets or sets the chosen row's id.</summary>
    internal string? SelectedId
    {
        get => GetValue(SelectedIdProperty);
        set => SetValue(SelectedIdProperty, value);
    }

    /// <summary>Gets or sets the error sentence.</summary>
    internal string? ErrorText
    {
        get => GetValue(ErrorTextProperty);
        set => SetValue(ErrorTextProperty, value);
    }

    /// <summary>Gets or sets the error code.</summary>
    internal string? ErrorCode
    {
        get => GetValue(ErrorCodeProperty);
        set => SetValue(ErrorCodeProperty, value);
    }

    /// <summary>Gets or sets the Cancel label.</summary>
    internal string? CancelText
    {
        get => GetValue(CancelTextProperty);
        set => SetValue(CancelTextProperty, value);
    }

    /// <summary>Puts focus on the first enabled row; the host calls it when the sheet opens.</summary>
    /// <returns>True when a row took focus.</returns>
    internal bool FocusFirst() => Enabled().FirstOrDefault()?.Focus(NavigationMethod.Tab) == true;

    /// <summary>Raises <see cref="Picked"/> for an item.</summary>
    /// <param name="item">The picked item.</param>
    internal void Pick(PickerItem item) => Picked?.Invoke(this, new ValueEventArgs<PickerItem>(item));

    /// <summary>Raises <see cref="Closed"/>.</summary>
    internal void Close() => Closed?.Invoke(this, EventArgs.Empty);

    private IEnumerable<SelectableTile> Enabled() => _tiles.Where(t => t.Item.IsEnabled).Select(t => t.Tile);

    private void ShowHeading()
    {
        _heading.Text = Heading;
        AutomationProperties.SetName(this, Heading);
    }

    private void ShowCancel()
    {
        _cancel.Content = CancelText;
        _cancel.IsVisible = !string.IsNullOrEmpty(CancelText);
        AutomationProperties.SetName(_cancel, CancelText);
    }

    private void ShowError()
    {
        var has = !string.IsNullOrEmpty(ErrorText);
        _error.IsVisible = has;
        _errorText.Text = ErrorText;
        AutomationProperties.SetName(_error, ErrorText);
        _details.IsVisible = has && !string.IsNullOrEmpty(ErrorCode);
        _code.Text = ErrorCode;
    }

    private void Rebuild()
    {
        _rows.Children.Clear();
        _tiles.Clear();
        foreach (var group in Groups ?? [])
        {
            if (group.Name.Length > 0)
            {
                var header = new TextBlock { Text = group.Name, Margin = new Thickness(0, 4, 0, 0) };
                header.Classes.Add("caption");
                header.Classes.Add("subtle");
                AutomationProperties.SetAccessibilityView(header, AccessibilityView.Raw);
                _rows.Children.Add(header);
            }

            foreach (var item in group.Items)
            {
                var tile = new SelectableTile { Content = item.Text, IsUnavailable = !item.IsEnabled, IsSelected = string.Equals(item.Id, SelectedId, StringComparison.Ordinal) };
                tile.Classes.Add("row");
                AutomationProperties.SetName(tile, item.Text);
                if (!item.IsEnabled)
                {
                    tile.Reason = item.Reason;
                    AutomationProperties.SetHelpText(tile, item.Reason);   // "label, unavailable, reason"
                }

                tile.Click += (_, _) =>
                {
                    if (item.IsEnabled)
                    {
                        Pick(item);
                    }
                };
                _tiles.Add((tile, item));
                _rows.Children.Add(tile);
            }
        }
    }

    private void MarkSelected()
    {
        foreach (var (tile, item) in _tiles)
        {
            tile.IsSelected = string.Equals(item.Id, SelectedId, StringComparison.Ordinal);
        }
    }

    // The picker owns Up, Down, Home, End and Esc; Enter and Space are the focused row's own button press. Tab is the host's trap.
    private void OnKeys(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        var rows = _tiles.Select(t => t.Tile).ToList();   // a disabled row takes focus too (#3651)
        var at = rows.FindIndex(t => t.IsFocused);
        SelectableTile? target = null;
        if (rows.Count > 0)
        {
            if (e.Key == Key.Down)
            {
                target = rows[Math.Min(at + 1, rows.Count - 1)];
            }
            else if (e.Key == Key.Up)
            {
                target = rows[Math.Max(at - 1, 0)];
            }
            else if (e.Key == Key.Home)
            {
                target = rows[0];
            }
            else if (e.Key == Key.End)
            {
                target = rows[^1];
            }
        }

        if (target is not null)
        {
            _ = target.Focus(NavigationMethod.Directional);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}
