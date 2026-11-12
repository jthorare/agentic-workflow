using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// The list picker (#3234): a heading, grouped selectable rows, an error line with its code under Details, and a Cancel button. It is the content of the shell's
/// sheet, so first focus and the focus trap come from that host; the picker gives the host <see cref="FocusFirst"/>. Up and Down move between the enabled rows
/// across groups, Home and End jump, Enter picks, Esc closes. The block picker and the Connect target list of the Workflows editor are both this part.
/// Stub: the layout and the keys come with the green commit.
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
    internal bool FocusFirst() => Content is not null;

    /// <summary>Raises <see cref="Picked"/> for an item.</summary>
    /// <param name="item">The picked item.</param>
    internal void Pick(PickerItem item) => Picked?.Invoke(this, new ValueEventArgs<PickerItem>(item));

    /// <summary>Raises <see cref="Closed"/>.</summary>
    internal void Close() => Closed?.Invoke(this, EventArgs.Empty);
}
