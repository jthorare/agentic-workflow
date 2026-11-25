using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// A button that is one choice out of several (#3234): the Add step block tile and the Connect target row of the Workflows editor. A button underneath, so Click, Command, Enter and Space,
/// focus and the accessible name come from the content. It has five looks: rest, hover, pressed, focus (a ring, for the keyboard only), selected (<see cref="IsSelected"/>) and disabled. Class "row" turns the
/// compact tile into a full-width row with left-aligned content. The template is in ControlStyles.axaml.
/// </summary>
[SuppressMessage("Design", "MA0182", Justification = "The Workflows editor adopts it in its own PR (#3234).")]
internal sealed class SelectableTile : Button
{
    /// <summary>True when this is the chosen one: a soft primary fill and a primary border.</summary>
    public static readonly StyledProperty<bool> IsSelectedProperty = AvaloniaProperty.Register<SelectableTile, bool>(nameof(IsSelected));

    /// <summary>True when the choice can't be taken right now (#3651): the tile still takes keyboard focus, so its reason can be reached, but pressing it does nothing. IsEnabled stays true.</summary>
    public static readonly StyledProperty<bool> IsUnavailableProperty = AvaloniaProperty.Register<SelectableTile, bool>(nameof(IsUnavailable));

    /// <summary>Why the choice is unavailable (#3651); shown under the label, in the secondary text style, whenever it is not empty.</summary>
    public static readonly StyledProperty<string?> ReasonProperty = AvaloniaProperty.Register<SelectableTile, string?>(nameof(Reason));

    private TextBlock? _reason;

    /// <summary>Gets or sets a value indicating whether this is the chosen one.</summary>
    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>Gets or sets why the choice is unavailable; empty shows nothing.</summary>
    public string? Reason
    {
        get => GetValue(ReasonProperty);
        set => SetValue(ReasonProperty, value);
    }

    /// <summary>Gets or sets a value indicating whether the choice can't be taken right now.</summary>
    public bool IsUnavailable
    {
        get => GetValue(IsUnavailableProperty);
        set => SetValue(IsUnavailableProperty, value);
    }

    static SelectableTile()
    {
        _ = ReasonProperty.Changed.AddClassHandler<SelectableTile>((tile, _) => tile.ShowReason());
        _ = IsUnavailableProperty.Changed.AddClassHandler<SelectableTile>((tile, e) => tile.PseudoClasses.Set(":unavailable", e.NewValue is true));
        _ = IsSelectedProperty.Changed.AddClassHandler<SelectableTile>((tile, e) =>
        {
            tile.PseudoClasses.Set(":selected", e.NewValue is true);
            (ControlAutomationPeer.FromElement(tile) as SelectableTileAutomationPeer)?.RaiseSelectedChanged(e.NewValue is true);
        });
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _reason = e.NameScope.Find<TextBlock>("PART_Reason");
        ShowReason();
    }

    /// <inheritdoc />
    protected override void OnClick()
    {
        if (!IsUnavailable)
        {
            base.OnClick();
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SelectableTileAutomationPeer(this);

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(SelectableTile);

    private void ShowReason()
    {
        if (_reason is not null)
        {
            _reason.Text = Reason;
            _reason.IsVisible = !string.IsNullOrEmpty(Reason);
        }
    }
}
