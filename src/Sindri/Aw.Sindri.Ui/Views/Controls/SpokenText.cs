using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// A line of text a screen reader reads as one Text (#3234): the Border-around-TextBlock pair the Workflows page repeats about forty times (a TextBlock's peer ignores
/// AutomationProperties.Name, so the name sits on a wrapper and the TextBlock is Raw). Its name is <see cref="Text"/>, or <see cref="Spoken"/> when that is set.
/// </summary>
[SuppressMessage("Design", "MA0182", Justification = "The Workflows page swaps its wrappers for it in its own PR (#3234).")]
internal sealed class SpokenText : Border
{
    /// <summary>The visible text; empty hides the line.</summary>
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<SpokenText, string?>(nameof(Text));

    /// <summary>What a screen reader reads when it should differ from the visible text; null reads the text.</summary>
    public static readonly StyledProperty<string?> SpokenProperty = AvaloniaProperty.Register<SpokenText, string?>(nameof(Spoken));

    /// <summary>The style classes of the inner text, space separated: "caption subtle".</summary>
    public static readonly StyledProperty<string?> TextClassesProperty = AvaloniaProperty.Register<SpokenText, string?>(nameof(TextClasses));

    /// <summary>Whether the text wraps.</summary>
    public static readonly StyledProperty<bool> WrapsProperty = AvaloniaProperty.Register<SpokenText, bool>(nameof(Wraps));

    private readonly TextBlock _text = new();
    private string[] _applied = [];

    static SpokenText()
    {
        _ = TextProperty.Changed.AddClassHandler<SpokenText>((t, _) => t.Refresh());
        _ = SpokenProperty.Changed.AddClassHandler<SpokenText>((t, _) => t.Refresh());
        _ = TextClassesProperty.Changed.AddClassHandler<SpokenText>((t, _) => t.Restyle());
        _ = WrapsProperty.Changed.AddClassHandler<SpokenText>((t, _) => t.Restyle());
    }

    /// <summary>Initializes a new instance of the <see cref="SpokenText"/> class, empty and hidden until it has text.</summary>
    public SpokenText()
    {
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Control);
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Text);
        AutomationProperties.SetAccessibilityView(_text, AccessibilityView.Raw);
        Child = _text;
        IsVisible = false;
        Restyle();
    }

    /// <summary>Gets or sets the visible text.</summary>
    internal string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Gets or sets what a screen reader reads.</summary>
    internal string? Spoken
    {
        get => GetValue(SpokenProperty);
        set => SetValue(SpokenProperty, value);
    }

    /// <summary>Gets or sets the style classes of the inner text.</summary>
    internal string? TextClasses
    {
        get => GetValue(TextClassesProperty);
        set => SetValue(TextClassesProperty, value);
    }

    /// <summary>Gets or sets a value indicating whether the text wraps.</summary>
    internal bool Wraps
    {
        get => GetValue(WrapsProperty);
        set => SetValue(WrapsProperty, value);
    }

    // The words and the one name: Spoken when set, else the text; no text hides the line and names nothing.
    private void Refresh()
    {
        _text.Text = Text;
        IsVisible = !string.IsNullOrEmpty(Text);
        AutomationProperties.SetName(this, string.IsNullOrEmpty(Text) ? null : Spoken ?? Text);
    }

    private void Restyle()
    {
        foreach (var name in _applied)
        {
            _ = _text.Classes.Remove(name);
        }

        _applied = (TextClasses ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var name in _applied)
        {
            _text.Classes.Add(name);
        }

        _text.TextWrapping = Wraps ? TextWrapping.Wrap : TextWrapping.NoWrap;
        _text.TextTrimming = Wraps ? TextTrimming.None : TextTrimming.CharacterEllipsis;
    }
}
