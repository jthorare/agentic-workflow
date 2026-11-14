using System.Xml.Linq;
using Aw.Kernel.Testing;

namespace Aw.Sindri.Ui.Tests.Theme;

/// <summary>UI spec §14.4: the Fluent dark palette maps to the tokens, and the control overrides use them.</summary>
[Lane(TestLane.Unit)]
public sealed class FluentOverrideTests
{
    private static readonly TokenFile _tokens = TokenFile.Load();
    private static readonly XDocument _overrides = XDocument.Load(Path.Combine(UiSource.Theme, "FluentOverrides.axaml"));

    public static TheoryData<string, string> Palette => new()
    {
        { "Accent", "Primary" },
        { "RegionColor", "Canvas" },
        { "AltHigh", "Canvas" },
        { "AltMedium", "Surface1" },
        { "AltLow", "Surface2" },
        { "BaseHigh", "Ink" },
        { "BaseMediumHigh", "InkMuted" },
        { "BaseMedium", "InkSubtle" },
        { "BaseLow", "Hairline" },
        { "ChromeHigh", "HairlineTertiary" },
        { "ChromeMedium", "Surface3" },
        { "ChromeLow", "Surface2" },
        { "ChromeMediumLow", "Surface1" },
        { "ChromeAltLow", "InkMuted" },
        { "ChromeGray", "InkSubtle" },
        { "ChromeDisabledHigh", "Hairline" },
        { "ChromeDisabledLow", "InkTertiary" },
        { "ListLow", "Surface2" },
        { "ListMedium", "Surface3" },
        { "ErrorText", "Error" },
    };

    [Theory]
    [MemberData(nameof(Palette))]
    public void Each_dark_palette_entry_is_its_token(string entry, string token)
    {
        var dark = _overrides.Descendants().Single(e => e.Name.LocalName == "ColorPaletteResources" && e.Attributes().Any(a => a.Name.LocalName == "Key" && a.Value == "Dark"));

        Assert.Equal(_tokens.Color(token), dark.Attribute(entry)?.Value.ToLowerInvariant());
    }

    // Resource overrides by key: each brush is its token's colour (hex is allowed only in Theme/, and this keeps it equal
    // to the token), and the corner radius is Radius.MD.
    [Theory]
    [InlineData("ButtonBackground", "Surface1")]
    [InlineData("ButtonBorderBrush", "Hairline")]
    [InlineData("ButtonForeground", "Ink")]
    [InlineData("ButtonBackgroundPointerOver", "Surface2")]
    [InlineData("ButtonBorderBrushPointerOver", "HairlineStrong")]
    [InlineData("ButtonForegroundPointerOver", "Ink")]
    [InlineData("ButtonForegroundPressed", "Ink")]
    [InlineData("ButtonForegroundDisabled", "InkTertiary")]
    [InlineData("AccentButtonBackground", "Primary")]
    [InlineData("AccentButtonBackgroundPointerOver", "PrimaryHoverFill")]
    [InlineData("AccentButtonBackgroundPressed", "PrimaryPressed")]
    [InlineData("AccentButtonForeground", "OnPrimary")]
    [InlineData("AccentButtonForegroundPointerOver", "OnPrimary")]
    [InlineData("AccentButtonForegroundPressed", "OnPrimary")]
    [InlineData("ToolTipBackground", "Surface3")]
    [InlineData("ToolTipBorderBrush", "Hairline")]
    [InlineData("ToolTipForeground", "Ink")]
    // A focused combo box keeps its dark face and readable text: Fluent fills it from the palette accent unless these say otherwise (#1080).
    [InlineData("ComboBoxBackground", "Surface1")]
    [InlineData("ComboBoxBackgroundPointerOver", "Surface2")]
    [InlineData("ComboBoxBackgroundUnfocused", "Surface1")]
    [InlineData("ComboBoxBackgroundBorderBrushFocused", "HairlineStrong")]
    [InlineData("ComboBoxForegroundFocused", "Ink")]
    [InlineData("ComboBoxForegroundFocusedPressed", "Ink")]
    [InlineData("ComboBoxPlaceHolderForegroundFocusedPressed", "InkMuted")]
    [InlineData("ComboBoxDropDownGlyphForegroundFocused", "Ink")]
    [InlineData("ComboBoxDropDownGlyphForegroundFocusedPressed", "Ink")]
    public void Each_brush_override_is_its_token(string key, string token) => Assert.Equal(_tokens.Color(token), Override(key)?.ToLowerInvariant());

    // The pressed fill of a Fluent button and combo box is the clickable token's colour (#3517); the key may hold the hex or a reference to the token.
    [Theory]
    [InlineData("ButtonBackgroundPressed")]
    [InlineData("ComboBoxBackgroundPressed")]
    public void Fluent_pressed_backgrounds_match_the_clickable_token(string key)
    {
        var token = _tokens.Resources["Brush.Clickable.Pressed"].Attribute("Color")!.Value.Trim().ToLowerInvariant();
        var value = Override(key)?.Trim().ToLowerInvariant();

        Assert.True(value == token || (value is not null && value.StartsWith('{') && value.EndsWith("brush.clickable.pressed}", StringComparison.Ordinal)), $"{key} is {value}, the token is {token}");
    }

    [Fact]
    public void Buttons_and_inputs_use_the_medium_radius() => Assert.Equal(_tokens.Resources["Radius.MD"].Value.Trim(), Override("ControlCornerRadius"));

    // §11.1's focus ring (2 px PrimaryHover, full opacity, offset 2 px) is checked as rendered, in Views/FocusRingTests.

    [Fact]
    public void Nothing_casts_a_shadow() =>
        Assert.DoesNotContain(_overrides.Descendants().Attributes(), a => a.Name.LocalName == "BoxShadow" || a.Value.Contains("BoxShadow", StringComparison.Ordinal));

    private static string? Override(string key) =>
        _overrides.Descendants().SingleOrDefault(e => e.Attributes().Any(a => a.Name.LocalName == "Key" && a.Value == key)) switch
        {
            null => null,
            var e when e.Attribute("Color") is { } colour => colour.Value,
            var e => e.Value.Trim() is { Length: > 0 } text ? text : e.Attributes().First(a => a.Name.LocalName != "Key").Value,
        };
}
