using System.Xml.Linq;
using Aw.Kernel.Testing;

namespace Aw.Sindri.Ui.Tests.Theme;

/// <summary>UI spec §14: Theme/Tokens.axaml declares every token with the value the spec maps from DESIGN.md.</summary>
[Lane(TestLane.Unit)]
public sealed class TokenTests
{
    private static readonly TokenFile _tokens = TokenFile.Load();

    // §14.1, every row with a resource key.
    public static TheoryData<string, string> Colours => new()
    {
        { "Canvas", "#010102" },
        { "Surface1", "#0f1011" },
        { "Surface2", "#141516" },
        { "Surface3", "#18191a" },
        { "Surface4", "#191a1b" },
        { "Hairline", "#23252a" },
        { "HairlineStrong", "#34343a" },
        { "HairlineTertiary", "#3e3e44" },
        { "Ink", "#f7f8f8" },
        { "InkMuted", "#d0d6e0" },
        { "InkSubtle", "#8a8f98" },
        { "InkTertiary", "#62666d" },
        { "Primary", "#5e6ad2" },
        { "PrimaryHover", "#828fff" },
        { "PrimaryHoverFill", "#535fc6" },
        { "PrimaryPressed", "#4c57be" },
        { "OnPrimary", "#ffffff" },
        { "Success", "#27a644" },
        { "Overlay", "#000000" },
        { "Attention", "#f2994a" },
        { "Error", "#eb5757" },
        { "AttentionSoft", "#23160c" },
        { "PrimarySoft", "#1d2036" },
        { "ErrorSoft", "#220d0e" },
        { "SuccessSoft", "#06180b" },
        { "Wire", "#4a4a52" },
        { "WireHi", "#8a8f98" },
        { "LaneFill", "#05ffffff" },
        { "RingAnchor", "#d0d6e0" },
    };

    [Theory]
    [MemberData(nameof(Colours))]
    public void Each_colour_has_its_value_and_a_brush_over_it(string name, string value)
    {
        Assert.Equal(value, _tokens.Color(name));
        var brush = _tokens.Resources["Brush." + name];
        Assert.Equal("SolidColorBrush", brush.Name.LocalName);
        Assert.Equal("{StaticResource Color." + name + "}", brush.Attribute("Color")?.Value);
    }

    [Fact]
    public void Every_colour_is_one_of_the_spec_tokens_and_every_brush_has_its_colour()
    {
        var spec = Colours.Select(row => row.Data.Item1).ToHashSet(StringComparer.Ordinal);
        var colours = Names("Color.");
        var brushes = Names("Brush.");

        Assert.Empty(colours.Except(spec, StringComparer.Ordinal));
        Assert.Equal(colours.Order(StringComparer.Ordinal), brushes.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void The_overlay_brush_is_the_scrim_at_60_percent() =>
        Assert.Equal("0.6", _tokens.Resources["Brush.Overlay"].Attribute("Opacity")?.Value);

    // §14.2: base sizes and line heights at scale 1.0, and one Double text-scale resource.
    public static TheoryData<string, double, double> Ramp => new()
    {
        { "PageTitle", 20, 24 },
        { "DetailTitle", 18, 22.5 },
        { "Body", 13, 19.5 },
        { "BodyStrong", 13, 19.5 },
        { "Caption", 12, 16.8 },
        { "Eyebrow", 11, 14.3 },
        { "Button", 13, 15.6 },
        { "Mono", 12, 18 },
        { "Kbd", 11, 14.3 },
    };

    [Theory]
    [MemberData(nameof(Ramp))]
    public void Each_type_step_has_its_size_and_line_height(string step, double size, double lineHeight)
    {
        Assert.Equal(size, _tokens.Number($"Type.{step}.Size"));
        Assert.Equal(lineHeight, _tokens.Number($"Type.{step}.LineHeight"), 3);
    }

    [Fact]
    public void The_text_scale_starts_at_one() => Assert.Equal(1.0, _tokens.Number("Type.Scale"));

    // §14.3.
    public static TheoryData<string, double> Numbers => new()
    {
        { "Space.XXS", 4 }, { "Space.XS", 8 }, { "Space.SM", 12 }, { "Space.MD", 16 }, { "Space.LG", 24 }, { "Space.XL", 32 }, { "Space.XXL", 48 },
        { "Size.Row", 36 }, { "Size.RowComfortable", 40 }, { "Size.TopBar", 48 }, { "Size.Sheet", 520 }, { "Size.SheetMin", 420 }, { "Size.SheetMax", 760 },
        { "Size.SheetCoversBelow", 1100 }, { "Size.WindowMinWidth", 900 }, { "Size.WindowMinHeight", 600 },
        { "Size.PanelGap", 16 }, { "Size.Control", 30 }, { "Size.FocusRing", 2 },
    };

    [Theory]
    [MemberData(nameof(Numbers))]
    public void Each_spacing_and_size_has_its_value(string key, double value) => Assert.Equal(value, _tokens.Number(key));

    [Theory]
    [InlineData("Radius.XS", "4")]
    [InlineData("Radius.SM", "6")]
    [InlineData("Radius.MD", "8")]
    [InlineData("Radius.LG", "12")]
    [InlineData("Radius.Pill", "9999")]
    [InlineData("Space.XS.Thickness", "8")]
    [InlineData("Space.MD.Thickness", "16")]
    [InlineData("Motion.Instant", "0:0:0")]
    [InlineData("Motion.Fast", "0:0:0.120")]
    [InlineData("Motion.Base", "0:0:0.180")]
    [InlineData("Motion.Pulse", "0:0:1.600")]
    [InlineData("Motion.Spin", "0:0:0.900")]
    public void Each_radius_thickness_and_duration_has_its_value(string key, string value) =>
        Assert.Equal(value, _tokens.Resources[key].Value.Trim());

    [Fact]
    public void The_standard_easing_is_the_spec_curve()
    {
        var easing = _tokens.Resources["Easing.Standard"];
        Assert.Equal(("0.2", "0", "0", "1"), (Attr(easing, "X1"), Attr(easing, "Y1"), Attr(easing, "X2"), Attr(easing, "Y2")));
    }

    [Fact]
    public void The_mono_family_is_the_embedded_JetBrains_Mono_with_system_fallbacks()
    {
        var mono = _tokens.Resources["Font.Mono"].Value.Trim();

        Assert.StartsWith("avares://Aw.Sindri.Ui/Theme/Fonts#JetBrains Mono", mono, StringComparison.Ordinal);
        Assert.EndsWith("Menlo, Consolas, monospace", mono, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(UiSource.Theme, "Fonts", "JetBrainsMono-Regular.ttf")));
        Assert.True(File.Exists(Path.Combine(UiSource.Theme, "Fonts", "JetBrainsMono-Medium.ttf")));
        Assert.Contains("SIL Open Font License", File.ReadAllText(Path.Combine(UiSource.Theme, "Fonts", "OFL.txt")), StringComparison.Ordinal);
    }

    // §14.2: one TextBlock style class per type step.
    [Theory]
    [InlineData("page-title")]
    [InlineData("detail-title")]
    [InlineData("body")]
    [InlineData("body-strong")]
    [InlineData("caption")]
    [InlineData("eyebrow")]
    [InlineData("button")]
    [InlineData("mono")]
    [InlineData("kbd")]
    public void Each_type_class_is_a_TextBlock_style(string name)
    {
        var styles = XDocument.Load(Path.Combine(UiSource.Theme, "Typography.axaml")).Descendants().Where(e => e.Name.LocalName == "Style");

        Assert.Contains(styles, s => s.Attribute("Selector")?.Value == "TextBlock." + name);
    }

    private static string? Attr(XElement e, string name) => e.Attribute(name)?.Value;

    private static List<string> Names(string prefix) =>
        [.. _tokens.Resources.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal) && !k[prefix.Length..].Contains('.', StringComparison.Ordinal)).Select(k => k[prefix.Length..])];
}
