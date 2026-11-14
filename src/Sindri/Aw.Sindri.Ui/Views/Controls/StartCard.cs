using Avalonia;
using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// The Workflows page's start card (#3234): a button that starts one way of making a workflow, with a title and a line under it. A button underneath, so Click, Command, Enter and Space, focus
/// and the accessible name come from the content. Six looks: rest, hover, pressed, focus (a ring, keyboard only), selected (<see cref="IsSelected"/>) and disabled, on the same tokens as
/// SelectableTile. It fills its column and has a card's padding. The template is in ControlStyles.axaml.
/// </summary>
#pragma warning disable MA0182 // the page uses it from XAML once ui swaps it in; the tests use it now
internal sealed class StartCard : Button
{
    /// <summary>True when this is the chosen one: a soft primary fill and a primary border.</summary>
    public static readonly StyledProperty<bool> IsSelectedProperty = AvaloniaProperty.Register<StartCard, bool>(nameof(IsSelected));

    /// <summary>Gets or sets a value indicating whether this is the chosen one.</summary>
    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    static StartCard()
    {
        _ = IsSelectedProperty.Changed.AddClassHandler<StartCard>((card, e) => card.PseudoClasses.Set(":selected", e.NewValue is true));
    }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(StartCard);
}
#pragma warning restore MA0182
