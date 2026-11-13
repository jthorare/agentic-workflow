using System.Xml.Linq;
using Aw.Kernel.Testing;

namespace Aw.Sindri.Ui.Tests.Theme;

/// <summary>UI spec §14: Theme/Tokens.axaml declares every token with the value the spec maps from DESIGN.md.</summary>
[Lane(TestLane.Unit)]
public sealed class TokenTests
{
    private static readonly TokenFile _tokens = TokenFile.Load();

    // §14.1, every row with a resource key.
    private static readonly (string, string)[] _colourRows =
    [
        ("Canvas", "#08090a"),
        ("Surface1", "#141517"),
        ("Surface2", "#1c1d20"),
        ("Surface3", "#222326"),
        ("Surface4", "#26272a"),
        ("Hairline", "#0fffffff"),
        ("HairlineStrong", "#34343a"),
        ("HairlineTertiary", "#3e3e44"),
        ("Outline", "#70757c"),   // control outlines, 3:1 non-text contrast (#3190)
        ("Ink", "#f7f8f8"),
        ("InkMuted", "#d0d6e0"),
        ("InkSubtle", "#8a8f98"),
        ("InkTertiary", "#74787f"),
        ("Primary", "#5e6ad2"),
        ("PrimaryHover", "#828fff"),
        ("PrimaryHoverFill", "#535fc6"),
        ("PrimaryPressed", "#4c57be"),
        ("OnPrimary", "#ffffff"),
        ("Success", "#2eb75b"),
        ("Overlay", "#000000"),
        ("Attention", "#f2994a"),
        ("Error", "#eb5757"),
        ("AttentionSoft", "#291d13"),
        ("AttentionSoft.Hover", "#32261c"),   // an attention row lifts within its own colour: AttentionSoft one step up
        ("AttentionSoft.Pressed", "#382c22"),   // and two steps up
        ("AttentionFill", "#67482f"),   // the hold button's fill: Attention at 35 % over Surface2
        ("PrimarySoft", "#202442"),
        ("ErrorSoft", "#281415"),
        ("ErrorChipText", "#ee6060"),
        ("SuccessSoft", "#0d2115"),
        ("Wire", "#686870"),
        ("WireHi", "#8a8f98"),
        ("LaneFill", "#05ffffff"),
        ("RingAnchor", "#d0d6e0"),
        ("Violet", "#a78bfa"),
        ("VioletSoft", "#1d1a29"),
        ("Area1", "#7aa2f7"),
        ("Area2", "#e0af68"),
        ("Area3", "#7bd88f"),
        ("Area4", "#c792ea"),
        ("Area5", "#5fd4e0"),
        ("Area6", "#ff9e8f"),
    ];

    public static TheoryData<string, string> Colours
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var r in _colourRows)
            {
                data.Add(r.Item1, r.Item2);
            }

            return data;
        }
    }

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
    public void A_clickable_surface_rests_on_the_hover_colour_at_zero_alpha()
    {
        // #2535: the colour ease blends each channel straight, so a rest fill of another colour (Transparent is #00FFFFFF) passes through it.
        var hover = _tokens.Resources["Brush.Clickable.Hover"].Attribute("Color")!.Value.Trim().ToLowerInvariant();
        var rest = _tokens.Resources["Brush.Clickable.Rest"].Attribute("Color")!.Value.Trim().ToLowerInvariant();

        Assert.Matches("^#[0-9a-f]{6}$", hover);
        Assert.Equal("#00" + hover[1..], rest);
    }

    [Fact]
    public void Every_clickable_surface_gets_its_rest_fill_before_any_surface_look()
    {
        // A Border with no background is not hit-tested (#2535). The fill is the first surface style in the app, so each surface's own fill,
        // declared later, replaces it, and ClickableStyles, last, lifts it.
        var app = XDocument.Load(Path.Combine(UiSource.Library, "Views", "SindriApp.axaml"));
        var styles = app.Descendants().Single(e => e.Name.LocalName == "Application.Styles").Elements().ToList();
        var rest = styles.FindIndex(e => e.Name.LocalName == "Style" && e.Attribute("Selector")?.Value == "Border.clickable");
        int Include(string file) => styles.FindIndex(e => e.Name.LocalName == "StyleInclude" && (e.Attribute("Source")?.Value.EndsWith(file, StringComparison.Ordinal) ?? false));

        Assert.True(rest >= 0, "SindriApp.axaml has no Border.clickable rest style");
        Assert.Equal("{DynamicResource Brush.Clickable.Rest}", styles[rest].Elements().Single(e => e.Attribute("Property")?.Value == "Background").Attribute("Value")?.Value);
        Assert.True(rest < Include("QueueStyles.axaml") && rest < Include("NeedsYouStyles.axaml"), "the rest fill comes after a surface look");
        Assert.True(rest < styles.FindIndex(e => e.Attribute("Selector")?.Value == "Border.card"), "the rest fill comes after Border.card");
        Assert.Equal(styles.Count - 1, Include("ClickableStyles.axaml"));
    }

    [Fact]
    public void The_overlay_brush_is_the_scrim_at_60_percent() =>
        Assert.Equal("0.6", _tokens.Resources["Brush.Overlay"].Attribute("Opacity")?.Value);

    // §14.2 and the 2026-11 measured pass (docs/csharp/sindri-ui-design.md): size, line height and weight at scale 1.0.
    public static TheoryData<string, double, double, string> Ramp => new()
    {
        { "Display", 40, 44, "SemiBold" },
        { "PageTitle", 24, 32, "SemiBold" },
        { "SectionTitle", 15, 22, "SemiBold" },
        { "DetailTitle", 20, 28, "SemiBold" },
        { "Body", 13, 20, "Normal" },
        { "BodyStrong", 13, 20, "Medium" },
        { "Caption", 12, 18, "Normal" },
        { "Eyebrow", 11, 16, "Normal" },
        { "Button", 13, 15.6, "Medium" },
        { "Mono", 12, 18, "Normal" },
        { "Kbd", 11, 16, "Medium" },
    };

    [Theory]
    [MemberData(nameof(Ramp))]
    public void Each_type_step_has_its_size_line_height_and_weight(string step, double size, double lineHeight, string weight)
    {
        Assert.Equal(size, _tokens.Number($"Type.{step}.Size"));
        Assert.Equal(lineHeight, _tokens.Number($"Type.{step}.LineHeight"), 3);
        Assert.Equal(weight, _tokens.Resources[$"Type.{step}.Weight"].Value.Trim());
    }

    [Theory]
    [InlineData("Compact", 28)]
    [InlineData("Default", 32)]
    [InlineData("Data", 36)]
    [InlineData("Comfortable", 40)]
    public void Each_row_height_has_its_value(string name, double height) => Assert.Equal(height, _tokens.Number($"Row.Height.{name}"));

    // Every TextBlock class reads its step's size, line height and weight from the tokens; no literal size is left.
    [Theory]
    [InlineData("display", "Display")]
    [InlineData("page-title", "PageTitle")]
    [InlineData("section-title", "SectionTitle")]
    [InlineData("detail-title", "DetailTitle")]
    [InlineData("body", "Body")]
    [InlineData("body-strong", "BodyStrong")]
    [InlineData("caption", "Caption")]
    [InlineData("eyebrow", "Eyebrow")]
    [InlineData("button", "Button")]
    [InlineData("mono", "Mono")]
    [InlineData("kbd", "Kbd")]
    public void Each_type_class_reads_its_tokens(string name, string step)
    {
        var style = TypographyStyles().Single(s => s.Attribute("Selector")?.Value == "TextBlock." + name);
        string? Setter(string property) => style.Elements().Single(e => Attr(e, "Property") == property).Attribute("Value")?.Value;

        Assert.Equal($"{{DynamicResource Type.{step}.Size}}", Setter("FontSize"));
        Assert.Equal($"{{DynamicResource Type.{step}.LineHeight}}", Setter("LineHeight"));
        Assert.Equal($"{{DynamicResource Type.{step}.Weight}}", Setter("FontWeight"));
    }

    // The inspora pass: tracking (px) per class, from the token PR spec.
    [Theory]
    [InlineData("display", "-0.8")]
    [InlineData("page-title", "-0.24")]
    [InlineData("section-title", "0")]
    [InlineData("body", "-0.13")]
    [InlineData("caption", "-0.24")]
    [InlineData("mono", "0")]
    public void Each_type_class_has_its_tracking(string name, string tracking)
    {
        var style = TypographyStyles().Single(s => s.Attribute("Selector")?.Value == "TextBlock." + name);

        Assert.Equal(tracking, style.Elements().Single(e => Attr(e, "Property") == "LetterSpacing").Attribute("Value")?.Value);
    }

    [Fact]
    public void The_chart_inset_is_Surface2_and_a_floating_layer_has_its_shadow()
    {
        Assert.Equal("{StaticResource Color.Surface2}", _tokens.Resources["Brush.Chart.Inset"].Attribute("Color")?.Value);
        Assert.Equal("0 8 24 0 #66000000", _tokens.Resources["Shadow.Float"].Value.Trim());
    }

    [Fact]
    public void A_card_has_no_border_and_Space_LG_padding_and_a_status_chip_is_Radius_SM()
    {
        var app = XDocument.Load(Path.Combine(UiSource.Library, "Views", "SindriApp.axaml"));
        var card = app.Descendants().Single(e => e.Name.LocalName == "Style" && e.Attribute("Selector")?.Value == "Border.card");
        string? Setter(string property) => card.Elements().Single(e => Attr(e, "Property") == property).Attribute("Value")?.Value;

        Assert.Equal("0", Setter("BorderThickness"));
        Assert.Equal("{StaticResource Space.LG.Thickness}", Setter("Padding"));
        Assert.DoesNotContain(card.Elements(), e => Attr(e, "Property") == "BorderBrush");

        var chip = XDocument.Load(Path.Combine(UiSource.Theme, "StatusChip.axaml")).Descendants()
            .First(e => e.Name.LocalName == "Style" && e.Attribute("Selector")?.Value == "Border.status-chip");
        Assert.Equal("{StaticResource Radius.SM}", chip.Elements().Single(e => Attr(e, "Property") == "CornerRadius").Attribute("Value")?.Value);
    }

    [Fact]
    public void Typography_has_no_literal_font_size_or_line_height() =>
        Assert.DoesNotContain(
            TypographyStyles().SelectMany(s => s.Elements()),
            e => (string.Equals(Attr(e, "Property"), "FontSize", StringComparison.Ordinal) || string.Equals(Attr(e, "Property"), "LineHeight", StringComparison.Ordinal) || string.Equals(Attr(e, "Property"), "FontWeight", StringComparison.Ordinal)) && !(Attr(e, "Value") ?? "").StartsWith("{DynamicResource", StringComparison.Ordinal));

    [Fact]
    public void The_text_scale_starts_at_one() => Assert.Equal(1.0, _tokens.Number("Type.Scale"));

    // §14.3.
    public static TheoryData<string, double> Numbers => new()
    {
        { "Space.XXS", 4 }, { "Space.XS", 8 }, { "Space.SM", 12 }, { "Space.MD", 16 }, { "Space.LG", 24 }, { "Space.XL", 32 }, { "Space.XXL", 48 },
        { "Size.PanelHeader", 36 }, { "Size.TopBar", 48 }, { "Size.Sheet", 520 }, { "Size.SheetMin", 420 }, { "Size.SheetMax", 760 },
        { "Size.SheetCoversBelow", 1100 }, { "Size.WindowMinWidth", 1000 }, { "Size.WindowMinHeight", 600 },
        { "Size.PanelGap", 16 }, { "Size.Control", 30 }, { "Size.FocusRing", 2 },
        { "Size.Inspector", 480 }, { "Size.InspectorMin", 360 }, { "Size.BoardMin", 1230 }, { "Size.LaneMin", 200 }, { "Size.LaneMinLast", 150 },
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
    [InlineData("Motion.Hover", "0:0:0.150")]
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
    [InlineData("display")]
    [InlineData("page-title")]
    [InlineData("section-title")]
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

    private static IEnumerable<XElement> TypographyStyles() =>
        XDocument.Load(Path.Combine(UiSource.Theme, "Typography.axaml")).Descendants().Where(e => e.Name.LocalName == "Style");

    private static string? Attr(XElement e, string name) => e.Attribute(name)?.Value;

    private static List<string> Names(string prefix) =>
        [.. _tokens.Resources.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal) && !k[prefix.Length..].Contains('.', StringComparison.Ordinal)).Select(k => k[prefix.Length..])];
}
