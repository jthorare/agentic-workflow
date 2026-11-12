using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// One workflow step as a card (#3234): the title, the block type, and the run's status as a <see cref="StateChipView"/> (glyph, word, tone), 200 by 64. Its look in every state
/// comes from <c>GraphTilePens</c>: hover, a focus ring outside the selected ring, disabled (dashed), error. A screen reader reads "title, type, status".
/// Stub: the layout and the states come with the green commit.
/// </summary>
[SuppressMessage("Design", "MA0182", Justification = "The editor canvas adopts it in its own PR (#3234).")]
internal sealed class GraphNodeCard : UserControl
{
    /// <summary>The step's title.</summary>
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<GraphNodeCard, string?>(nameof(Title));

    /// <summary>The block type, such as "run_agent".</summary>
    public static readonly StyledProperty<string?> KindProperty = AvaloniaProperty.Register<GraphNodeCard, string?>(nameof(Kind));

    /// <summary>The status word of the run shown; empty hides the chip.</summary>
    public static readonly StyledProperty<string?> StatusWordProperty = AvaloniaProperty.Register<GraphNodeCard, string?>(nameof(StatusWord));

    /// <summary>The status tone: ok, running, waiting, attention, error, neutral or draft.</summary>
    public static readonly StyledProperty<string?> StatusToneProperty = AvaloniaProperty.Register<GraphNodeCard, string?>(nameof(StatusTone));

    /// <summary>The glyph before the status word.</summary>
    public static readonly StyledProperty<string?> StatusGlyphProperty = AvaloniaProperty.Register<GraphNodeCard, string?>(nameof(StatusGlyph));

    /// <summary>Whether this is the selected step (the inner ring).</summary>
    public static readonly StyledProperty<bool> IsSelectedProperty = AvaloniaProperty.Register<GraphNodeCard, bool>(nameof(IsSelected));

    /// <summary>Whether the step failed or is invalid (the error fill and border).</summary>
    public static readonly StyledProperty<bool> IsInvalidProperty = AvaloniaProperty.Register<GraphNodeCard, bool>(nameof(IsInvalid));

    /// <summary>Gets or sets the title.</summary>
    internal string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Gets or sets the block type.</summary>
    internal string? Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>Gets or sets the status word.</summary>
    internal string? StatusWord
    {
        get => GetValue(StatusWordProperty);
        set => SetValue(StatusWordProperty, value);
    }

    /// <summary>Gets or sets the status tone.</summary>
    internal string? StatusTone
    {
        get => GetValue(StatusToneProperty);
        set => SetValue(StatusToneProperty, value);
    }

    /// <summary>Gets or sets the status glyph.</summary>
    internal string? StatusGlyph
    {
        get => GetValue(StatusGlyphProperty);
        set => SetValue(StatusGlyphProperty, value);
    }

    /// <summary>Gets or sets a value indicating whether this is the selected step.</summary>
    internal bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>Gets or sets a value indicating whether the step failed or is invalid.</summary>
    internal bool IsInvalid
    {
        get => GetValue(IsInvalidProperty);
        set => SetValue(IsInvalidProperty, value);
    }
}
