using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Aw.Kernel.Testing;

namespace Aw.Sindri.Ui.Tests.Theme;

/// <summary>
/// The guard behind #3190: a border a control draws in a required state (pressed, focus, selected, checked) is a state indicator, so it holds 3:1 against the
/// Canvas and against Surface1 (WCAG 1.4.11) and is never a Hairline colour. Rest and hover borders are exempt: hover is not a required state. The test reads every
/// style under src/Sindri/Aw.Sindri.Ui whose selector names one of those states and resolves each BorderBrush setter through Theme/Tokens.axaml.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class StateBorderContrastTests
{
    /// <summary>How a style's selector relates to the 3:1 rule (#3490): a required state, one the rule exempts, a pseudo-class nobody classified, or no state at all.</summary>
    public enum SelectorState
    {
        None,
        Required,
        Exempt,
        Unknown,
    }

#pragma warning disable IDE0060 // the red stub reads nothing
    /// <summary>Reads a style selector's pseudo-classes. Stub: the red commit compiles and classifies nothing.</summary>
    internal static class StateSelectors
    {
        internal static SelectorState Classify(string selector) => SelectorState.None;

        internal static IReadOnlyList<string> UnknownIn(string fileName, XElement style) => [];
    }
#pragma warning restore IDE0060

    private const double NonText = 3;

    private static readonly TokenFile _tokens = TokenFile.Load();

    private static readonly Regex _state = new(@":pressed|:focus|:checked|:selected|\.selected", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Regex _brush = new(@"^\{(?:Dynamic|Static)Resource (?<key>Brush\.[\w.]+)\}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    [Fact]
    public void Every_state_border_holds_3_to_1_on_the_canvas_and_on_surface_1_and_is_never_a_hairline()
    {
        var borders = StateBorders().ToList();
        var failures = new List<string>();
        foreach (var (where, key) in borders)
        {
            if (!_tokens.Resources.ContainsKey(key))
            {
                failures.Add(where + " -> " + key + " is not in Theme/Tokens.axaml");
                continue;
            }

            if (ColourName(key).StartsWith("Hairline", StringComparison.Ordinal))
            {
                failures.Add(where + " -> " + key + " is a Hairline colour");
                continue;
            }

            var argb = Argb(key);
            var (onCanvas, onSurface) = (Wcag.Contrast(argb, _tokens.Color("Canvas")), Wcag.Contrast(argb, _tokens.Color("Surface1")));
            if (onCanvas < NonText || onSurface < NonText)
            {
                failures.Add(string.Create(CultureInfo.InvariantCulture, $"{where} -> {key}: {onCanvas:F2}:1 on Canvas, {onSurface:F2}:1 on Surface1"));
            }
        }

        // The scan reads real styles: the pressed clickable border and the focus ring are among them.
        var keys = borders.Select(b => b.Key).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("Brush.Clickable.PressedBorder", keys);
        Assert.Contains("Brush.PrimaryHover", keys);
        Assert.Contains(borders, b => b.Where.EndsWith(": Button.segment.selected", StringComparison.Ordinal));
        Assert.DoesNotContain(borders, b => b.Where.Contains("Button.command-control.primary:pressed", StringComparison.Ordinal) && string.Equals(b.Key, "Brush.PrimaryPressed", StringComparison.Ordinal));
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Distinct(StringComparer.Ordinal)));
    }

    [Theory]
    [InlineData("Button.x:pressed", SelectorState.Required)]
    [InlineData("Border.item-row:focus-visible", SelectorState.Required)]
    [InlineData("Border.review-row:focus-within, Border.review-row:focus", SelectorState.Required)]
    [InlineData("Button.segment.selected", SelectorState.Required)]
    [InlineData("Button.command-control.primary:pointerover:not(:disabled) /template/ ContentPresenter#PART_ContentPresenter", SelectorState.Exempt)]
    [InlineData("Expander:expanded /template/ Border", SelectorState.Unknown)]
    [InlineData("Border.card", SelectorState.None)]
    public void A_selector_is_required_exempt_unknown_or_no_state(string selector, SelectorState expected) =>
        Assert.Equal(expected, StateSelectors.Classify(selector));

    [Fact]
    public void A_border_brush_under_a_pseudo_class_nobody_classified_is_a_guard_failure_naming_file_line_and_class()
    {
        var style = XElement.Parse("<Style Selector=\"Expander:expanded /template/ Border\">\n  <Setter Property=\"BorderBrush\" Value=\"{DynamicResource Brush.Outline}\" />\n</Style>", LoadOptions.SetLineInfo);

        var failures = StateSelectors.UnknownIn("Some.axaml", style);

        var failure = Assert.Single(failures);
        Assert.Contains("Some.axaml:1", failure, StringComparison.Ordinal);
        Assert.Contains(":expanded", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void A_style_with_no_border_brush_is_never_a_guard_failure_whatever_its_pseudo_class()
    {
        var style = XElement.Parse("<Style Selector=\"Expander:expanded\"><Setter Property=\"Background\" Value=\"{DynamicResource Brush.Surface2}\" /></Style>", LoadOptions.SetLineInfo);

        Assert.Empty(StateSelectors.UnknownIn("Some.axaml", style));
    }

    // Every (file: selector, brush key) of a BorderBrush setter in a style whose selector names a required state.
    private static IEnumerable<(string Where, string Key)> StateBorders() =>
        Directory.EnumerateFiles(UiSource.Library, "*.axaml", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file))
            .SelectMany(file => XDocument.Load(file).Descendants()
                .Where(e => string.Equals(e.Name.LocalName, "Style", StringComparison.Ordinal))
                .SelectMany(style => BordersOf(Path.GetFileName(file), style)));

    private static bool IsBuildOutput(string file) =>
        file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
        || file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    // The state style's BorderBrush keys. A border painted the same brush as its own fill is not a separate indicator:
    // the pressed primary button (Button.command-control.primary:pressed, Border and Background both Brush.PrimaryPressed) is the case.
    private static IEnumerable<(string Where, string Key)> BordersOf(string fileName, XElement style)
    {
        var selector = style.Attribute("Selector")?.Value ?? string.Empty;
        if (!_state.IsMatch(selector))
        {
            return [];
        }

        var setters = style.Elements().Where(e => string.Equals(e.Name.LocalName, "Setter", StringComparison.Ordinal)).ToList();
        var background = SetterValue(setters, "Background");
        return BorderValues(setters, background)
            .Select(BrushKey)
            .OfType<string>()
            .Select(key => (fileName + ": " + selector, key));
    }

    private static string? SetterValue(List<XElement> setters, string property) =>
        setters.FirstOrDefault(e => string.Equals(e.Attribute("Property")?.Value, property, StringComparison.Ordinal))?.Attribute("Value")?.Value;

    // The BorderBrush values that differ from the fill.
    private static IEnumerable<string> BorderValues(List<XElement> setters, string? background) =>
        setters
            .Where(e => e.Attribute("Property")?.Value.EndsWith("BorderBrush", StringComparison.Ordinal) ?? false)
            .Select(e => e.Attribute("Value")?.Value ?? string.Empty)
            .Where(v => !string.Equals(v, background, StringComparison.Ordinal));

    private static string? BrushKey(string value)
    {
        var match = _brush.Match(value);
        return match.Success ? match.Groups["key"].Value : null;
    }

    // The Color.<name> a brush is over ("Outline"), or the literal colour's own text.
    private static string ColourName(string brushKey)
    {
        const string prefix = "{StaticResource Color.";
        var colour = _tokens.Resources[brushKey].Attribute("Color")?.Value.Trim() ?? string.Empty;
        return colour.StartsWith(prefix, StringComparison.Ordinal) ? colour[prefix.Length..^1] : colour;
    }

    // The brush as #aarrggbb: its colour's own alpha times the brush's Opacity.
    private static string Argb(string brushKey)
    {
        var name = ColourName(brushKey);
        var hex = (name.StartsWith('#') ? name : _tokens.Color(name)).TrimStart('#').ToLowerInvariant();
        var own = hex.Length == 8 ? int.Parse(hex.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0 : 1;
        var opacity = double.Parse(_tokens.Resources[brushKey].Attribute("Opacity")?.Value ?? "1", CultureInfo.InvariantCulture);
        var alpha = (int)Math.Round(own * opacity * 255, MidpointRounding.AwayFromZero);
        return "#" + alpha.ToString("x2", CultureInfo.InvariantCulture) + hex[^6..];
    }
}
