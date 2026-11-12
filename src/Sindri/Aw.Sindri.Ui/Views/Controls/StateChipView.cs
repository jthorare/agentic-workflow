using Avalonia;
using Avalonia.Controls;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// The state chip: a small bordered word, coloured by its tone. The word is drawn as served; the tone picks the class
/// (attn, fail, ok, or none for neutral). The served chip wins; a chip the client draws itself is the fallback.
/// </summary>
internal sealed class StateChipView : Border
{
    /// <summary>The chip the API served.</summary>
    public static readonly StyledProperty<StateChip?> ChipProperty = AvaloniaProperty.Register<StateChipView, StateChip?>(nameof(Chip));

    private static readonly string[] _tones = ["attn", "fail", "ok"];

    private readonly TextBlock _word = new();

    static StateChipView()
    {
        _ = ChipProperty.Changed.AddClassHandler<StateChipView>((chip, _) => chip.Apply());
        _ = FallbackProperty.Changed.AddClassHandler<StateChipView>((chip, _) => chip.Apply());
    }

    /// <summary>Initializes a new instance of the <see cref="StateChipView"/> class, empty and hidden until it has a chip to draw.</summary>
    public StateChipView()
    {
        Classes.Add("chip");
        Child = _word;
        IsVisible = false;
    }

    /// <summary>The chip a view draws itself when none is served.</summary>
    public static readonly StyledProperty<StateChip?> FallbackProperty = AvaloniaProperty.Register<StateChipView, StateChip?>(nameof(Fallback));

    /// <summary>The word of a chip a view names itself (a workflow's or a run's state). Wins over <see cref="Chip"/> when set.</summary>
    public static readonly StyledProperty<string?> WordProperty = AvaloniaProperty.Register<StateChipView, string?>(nameof(Word));

    /// <summary>The tone of a self-named chip: ok, running, waiting, attention, error, neutral or draft (the status-chip set of Theme/StatusChip.axaml).</summary>
    public static readonly StyledProperty<string?> ToneProperty = AvaloniaProperty.Register<StateChipView, string?>(nameof(Tone));

    /// <summary>An optional glyph drawn before the word; the screen reader still reads only the word.</summary>
    public static readonly StyledProperty<string?> GlyphProperty = AvaloniaProperty.Register<StateChipView, string?>(nameof(Glyph));

    /// <summary>Gets or sets the word of a self-named chip.</summary>
    public string? Word
    {
        get => GetValue(WordProperty);
        set => SetValue(WordProperty, value);
    }

    /// <summary>Gets or sets the tone of a self-named chip.</summary>
    public string? Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    /// <summary>Gets or sets the glyph before the word.</summary>
    public string? Glyph
    {
        get => GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    /// <summary>Gets or sets the served chip.</summary>
    public StateChip? Chip
    {
        get => GetValue(ChipProperty);
        set => SetValue(ChipProperty, value);
    }

    /// <summary>Gets or sets the chip a view draws itself.</summary>
    public StateChip? Fallback
    {
        get => GetValue(FallbackProperty);
        set => SetValue(FallbackProperty, value);
    }

    // The word as served, and the one class its tone picks; no chip at all draws nothing and takes no room.
    private void Apply()
    {
        var view = ChipView.Resolve(Chip, Fallback);
        foreach (var tone in _tones)
        {
            _ = Classes.Remove(tone);
        }

        IsVisible = view is not null;
        _word.Text = view?.Word;
        Avalonia.Automation.AutomationProperties.SetName(_word, view?.Word);
        if (view?.ToneClass is { } toneClass)
        {
            Classes.Add(toneClass);
        }
    }
}
