using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
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
    private static readonly string[] _statusTones = ["ok", "running", "waiting", "attention", "error", "neutral", "draft"];

    private readonly TextBlock _word = new();
    private readonly TextBlock _glyph = new() { Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _row = new() { Orientation = Orientation.Horizontal };

    static StateChipView()
    {
        _ = ChipProperty.Changed.AddClassHandler<StateChipView>((chip, _) => chip.Apply());
        _ = FallbackProperty.Changed.AddClassHandler<StateChipView>((chip, _) => chip.Apply());
        _ = WordProperty.Changed.AddClassHandler<StateChipView>((chip, _) => chip.Apply());
        _ = ToneProperty.Changed.AddClassHandler<StateChipView>((chip, _) => chip.Apply());
        _ = GlyphProperty.Changed.AddClassHandler<StateChipView>((chip, _) => chip.Apply());
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
    // A chip a view names itself (Word or Tone set) uses the status-chip set instead: class status plus status-<tone>.
    private void Apply()
    {
        foreach (var tone in _tones)
        {
            _ = Classes.Remove(tone);
        }

        foreach (var tone in _statusTones)
        {
            _ = Classes.Remove("status-" + tone);
        }

        _ = Classes.Remove("status");
        if (Word is not null)
        {
            ApplyStatus();
            return;
        }

        var view = ChipView.Resolve(Chip, Fallback);
        SetChild(glyph: null);
        IsVisible = view is not null;
        _word.Text = view?.Word;
        Avalonia.Automation.AutomationProperties.SetName(_word, view?.Word);
        Avalonia.Automation.AutomationProperties.SetName(this, view?.Word);
        if (view?.ToneClass is { } toneClass)
        {
            Classes.Add(toneClass);
        }
    }

    /// <summary>Paints the chip with a host's own brushes instead of its tone's (a disabled graph card dims its chip with itself). The word, the tone and the glyph stay as set.</summary>
    /// <param name="fill">The chip's fill.</param>
    /// <param name="border">The chip's border.</param>
    /// <param name="ink">The word's colour.</param>
    internal void Dim(IBrush fill, IBrush border, IBrush ink)
    {
        Background = fill;
        BorderBrush = border;
        _word.Foreground = ink;
        _glyph.Foreground = ink;
    }

    /// <summary>Gives the chip its tone's paint back.</summary>
    internal void ClearDim()
    {
        ClearValue(BackgroundProperty);
        ClearValue(BorderBrushProperty);
        _word.ClearValue(TextBlock.ForegroundProperty);
        _glyph.ClearValue(TextBlock.ForegroundProperty);
    }

    private void ApplyStatus()
    {
        var tone = _statusTones.Contains(Tone, StringComparer.Ordinal) ? Tone : "neutral";
        Classes.Add("status");
        Classes.Add("status-" + tone);
        IsVisible = true;
        _word.Text = Word;
        Avalonia.Automation.AutomationProperties.SetName(_word, Word);
        Avalonia.Automation.AutomationProperties.SetName(this, Word);
        SetChild(string.IsNullOrEmpty(Glyph) ? null : Glyph);
    }

    // The word alone is the child; with a glyph the child is a row of glyph and word. The glyph is decoration: the word carries the meaning.
    private void SetChild(string? glyph)
    {
        if (glyph is null)
        {
            if (!ReferenceEquals(Child, _word))
            {
                _row.Children.Clear();
                Child = _word;
            }

            return;
        }

        _glyph.Text = glyph;
        Avalonia.Automation.AutomationProperties.SetAccessibilityView(_glyph, Avalonia.Automation.AccessibilityView.Raw);
        if (!ReferenceEquals(Child, _row))
        {
            Child = null;
            _row.Children.Clear();
            _row.Children.Add(_glyph);
            _row.Children.Add(_word);
            Child = _row;
        }
    }
}
