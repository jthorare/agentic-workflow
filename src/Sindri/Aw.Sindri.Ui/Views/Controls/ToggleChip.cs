#pragma warning disable MA0182 // until a page places the control

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// The toggle chip (#3234): one small bordered word that is either a toggle (the Advanced mode switch: hover, pressed, focus ring, checked) or,
/// with <see cref="Interactive"/> off, a status chip that cannot be clicked or focused (the mode chip, the findings chip, a run row's Test
/// chip). The tone is the status-chip set of Theme/StatusChip.axaml, the same brushes StateChipView draws. The look is in Views/Controls/ControlStyles.axaml.
/// </summary>
internal sealed class ToggleChip : ToggleButton
{
    /// <summary>The word the chip shows and the screen reader reads.</summary>
    public static readonly StyledProperty<string?> WordProperty = AvaloniaProperty.Register<ToggleChip, string?>(nameof(Word));

    /// <summary>The tone: ok, running, waiting, attention, error, neutral or draft.</summary>
    public static readonly StyledProperty<string> ToneProperty = AvaloniaProperty.Register<ToggleChip, string>(nameof(Tone), "neutral");

    /// <summary>Whether the chip can be clicked and focused; off, it is a status chip and reads as text.</summary>
    public static readonly StyledProperty<bool> InteractiveProperty = AvaloniaProperty.Register<ToggleChip, bool>(nameof(Interactive), defaultValue: true);

    private static readonly string[] _tones = ["ok", "running", "waiting", "attention", "error", "neutral", "draft"];

    private readonly TextBlock _word = new();

    // The state dot before the word: hollow when the chip is off, filled when it is on (the cue that is not colour alone). Its colours come from the styles, never from a local value.
    private readonly Ellipse _dot = new() { Name = "PART_Dot", Width = 8, Height = 8, StrokeThickness = 1, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };

    static ToggleChip()
    {
        _ = WordProperty.Changed.AddClassHandler<ToggleChip>((chip, _) => chip.Apply());
        _ = ToneProperty.Changed.AddClassHandler<ToggleChip>((chip, _) => chip.Apply());
        _ = InteractiveProperty.Changed.AddClassHandler<ToggleChip>((chip, _) => chip.Apply());
    }

    /// <summary>Initializes a new instance of the <see cref="ToggleChip"/> class.</summary>
    public ToggleChip()
    {
        Classes.Add("toggle-chip");
        AutomationProperties.SetAccessibilityView(_dot, AccessibilityView.Raw);
        Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { _dot, _word } };
        Apply();
    }

    /// <summary>Gets the style key: a ToggleChip takes the ToggleButton theme and its styles (Avalonia matches a type selector on the style key, not on inheritance).</summary>
    protected override Type StyleKeyOverride => typeof(ToggleButton);

    /// <summary>Gets or sets the word the chip shows and the screen reader reads.</summary>
    public string? Word
    {
        get => GetValue(WordProperty);
        set => SetValue(WordProperty, value);
    }

    /// <summary>Gets or sets the tone.</summary>
    public string Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    /// <summary>Gets or sets a value indicating whether the chip can be clicked and focused.</summary>
    public bool Interactive
    {
        get => GetValue(InteractiveProperty);
        set => SetValue(InteractiveProperty, value);
    }

    // The word, one tone class (an unknown tone is neutral), and the interactive class; a status chip takes neither pointer nor focus and reads as text.
    private void Apply()
    {
        foreach (var tone in _tones)
        {
            _ = Classes.Remove("status-" + tone);
        }

        Classes.Add("status-" + (_tones.Contains(Tone, StringComparer.Ordinal) ? Tone : "neutral"));
        _ = Classes.Remove("interactive");
        if (Interactive)
        {
            Classes.Add("interactive");
        }

        _word.Text = Word;
        AutomationProperties.SetName(this, Word);
        Focusable = Interactive;
        IsHitTestVisible = Interactive;
        AutomationProperties.SetControlTypeOverride(this, Interactive ? null : Avalonia.Automation.Peers.AutomationControlType.Text);
    }
}
