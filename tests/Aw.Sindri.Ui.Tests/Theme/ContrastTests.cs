using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Aw.Kernel.Testing;

namespace Aw.Sindri.Ui.Tests.Theme;

/// <summary>
/// UI spec §11.1: every foreground/background pair the app uses, computed from Theme/Tokens.axaml's values, against
/// the ratio the spec records and the WCAG 2.2 threshold for its use.
/// </summary>
[Lane(TestLane.Unit)]
public sealed partial class ContrastTests
{
    private const double TextAa = 4.5;
    private const double NonText = 3.0;

    private static readonly TokenFile _tokens = TokenFile.Load();

    // (foreground, background, the ratio §11.1 records, the minimum its use needs; 0 = decoration or disabled, no minimum)
    private static readonly (string, string, double, double)[] _pairRows =
    [
        ("Ink", "Canvas", 18.7, TextAa),
        ("Ink", "Surface1", 17.2, TextAa),
        ("Ink", "Surface2", 15.8, TextAa),
        ("InkMuted", "Canvas", 13.6, TextAa),
        ("InkMuted", "Surface1", 12.5, TextAa),
        ("InkSubtle", "Canvas", 6.1, TextAa),
        ("InkSubtle", "Surface1", 5.6, TextAa),
        ("InkSubtle", "Surface2", 5.2, TextAa),
        ("InkTertiary", "Canvas", 4.5, 0),
        ("InkTertiary", "Surface1", 4.1, 0),
        ("PrimaryHover", "Canvas", 7.0, TextAa),
        ("PrimaryHover", "Surface1", 6.4, NonText),
        ("PrimaryHover", "Surface3", 5.5, NonText),   // the palette's selected-row bar on its card (U1 run 1, a11y-4)
        ("OnPrimary", "Primary", 4.70, TextAa),
        ("OnPrimary", "PrimaryHoverFill", 5.5, TextAa),
        ("OnPrimary", "PrimaryPressed", 6.2, TextAa),
        ("Error", "Canvas", 5.7, TextAa),
        ("Error", "Surface1", 5.3, TextAa),   // the todo row's Overdue due word on the panel (todo spec §3.5)
        ("Success", "Canvas", 7.6, TextAa),
        ("Attention", "Canvas", 9.0, TextAa),
        ("Attention", "Surface1", 8.2, TextAa),
        ("Attention", "AttentionSoft", 7.4, TextAa),   // the attn chip
        ("Error", "ErrorSoft", 5.0, TextAa),   // the fail chip
        ("Success", "SuccessSoft", 6.5, TextAa),   // the ok chip
        ("InkMuted", "Surface2", 11.5, TextAa),   // the neutral chip
        ("Ink", "AttentionSoft.Hover", 13.8, TextAa),   // a Needs You row's text under the pointer (#2611)
        ("Ink", "AttentionSoft.Pressed", 12.7, TextAa),   // and while pressed
        ("Ink", "AttentionFill", 7.8, TextAa),   // the hold button's label over its fill (setup spec §2.3 says about 9; the computed ratio is 7.8)
        ("AttentionFill", "Surface2", 2.05, 0),   // decoration: the fill against the button's own background
        ("Hairline", "Canvas", 1.1, 0),
        ("HairlineStrong", "Canvas", 1.6, 0),
        // The wire (#3138): a workflow graph's connectors and the context map's edges, ports and suggested borders. It is
        // the only mark of what feeds what, so it needs the non-text 3.0 on every surface a graph or map draws on. The
        // highlighted wire and the anchor ring are the same values as InkSubtle and InkMuted, so they pass as non-text.
        ("Wire", "Canvas", 3.6, NonText),
        ("Wire", "Surface1", 3.3, NonText),
        ("Wire", "Surface2", 3.05, NonText),
        ("WireHi", "Canvas", 6.1, NonText),
        ("WireHi", "Surface1", 5.6, NonText),
        ("RingAnchor", "Canvas", 13.6, NonText),
        ("RingAnchor", "Surface1", 12.5, NonText),
    ];

    public static TheoryData<string, string, double, double> Pairs
    {
        get
        {
            var data = new TheoryData<string, string, double, double>();
            foreach (var r in _pairRows)
            {
                data.Add(r.Item1, r.Item2, r.Item3, r.Item4);
            }

            return data;
        }
    }

    // The todo panel's colours (todo spec D6, §3.5). The theme is dark only, so there is one set of rows.
    public static TheoryData<string, string, double, double> TodoPairs => new()
    {
        // The region focus ring (todo spec D6): Todo panel and Home's Needs you panel draw a 1 px Primary border on the panel (Surface1).
        // The border is a shape cue beside the accent heading, so it needs the non-text 3.0.
        { "Primary", "Surface1", 3.9, NonText },
        { "Primary", "Canvas", 4.2, NonText },   // the ring where a panel border meets the page
        { "Error", "Surface2", 4.8, TextAa },   // the Overdue word on a selected todo row (item-row.selected is Surface2)
    };

    // The workflow run statuses (F3 workflows spec §2), drawn on a board card (Surface2) and in the run drawer (Surface1).
    public static TheoryData<string, string, double, double> StatusPairs => new()
    {
        // The workflow run's "you took over" status (F3 workflows spec §2 "Tokens"): violet glyph and word on its soft pair.
        { "Violet", "VioletSoft", 6.3, TextAa },
        { "Violet", "Surface1", 6.7, TextAa },
        { "Violet", "Surface2", 6.2, TextAa },
        // The other status pairs the Workflows page draws on a card (Surface2) and in a drawer (Surface1).
        { "PrimaryHover", "PrimarySoft", 5.3, TextAa },   // running
        { "Attention", "Surface2", 7.6, TextAa },
        { "Error", "Surface2", 4.8, TextAa },
        { "Success", "Surface2", 6.5, TextAa },
        { "InkMuted", "Surface3", 10.8, TextAa },   // cancelled
        { "InkSubtle", "Surface3", 4.8, TextAa },   // not started
    };

    // Control outlines are UI parts: 3:1 (WCAG 1.4.11, #3190). The rows name the brush the control draws (Brush.<name>), so the fix may
    // repoint it at any colour. The recorded ratios are the ones #70757c has on the canvas, the panel and the pressed fill (#222326);
    // #5e6268 would pass the first two but measures only 2.6 on the pressed fill.
    public static TheoryData<string, string, double, double> OutlinePairs => new()
    {
        { "Clickable.PressedBorder", "Canvas", 4.29, NonText },
        { "Clickable.PressedBorder", "Surface1", 3.94, NonText },
        { "Clickable.PressedBorder", "Brush.Clickable.Pressed", 3.39, NonText },
        // Pressed and selected borders (inbox-row.selected, Button.segment.selected, the pressed hold part and primary) draw Brush.Clickable.PressedBorder on the other surfaces a clickable sits on (#3504).
        { "Clickable.PressedBorder", "Surface2", 3.63, NonText },
        { "Clickable.PressedBorder", "Surface3", 3.39, NonText },
        { "Clickable.PressedBorder", "Surface4", 3.22, NonText },
    };

    [Theory]
    [MemberData(nameof(OutlinePairs))]
    public void Each_control_outline_has_the_recorded_ratio_and_meets_the_non_text_minimum(string brush, string background, double recorded, double minimum)
    {
        var ratio = Wcag.Contrast(BrushColor(brush), Background(background));

        Assert.True(Math.Abs(ratio - recorded) <= 0.1, string.Create(CultureInfo.InvariantCulture, $"Brush.{brush} on {background} is {ratio:F2}, #3190 records {recorded}"));
        Assert.True(ratio >= minimum, string.Create(CultureInfo.InvariantCulture, $"Brush.{brush} on {background} is {ratio:F2}, below {minimum}"));
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    [MemberData(nameof(StatusPairs))]
    [MemberData(nameof(TodoPairs))]
    public void Each_pair_has_the_recorded_ratio_and_meets_its_threshold(string foreground, string background, double recorded, double minimum)
    {
        var ratio = _tokens.Contrast(foreground, background);

        Assert.True(Math.Abs(ratio - recorded) <= 0.1, string.Create(CultureInfo.InvariantCulture, $"{foreground} on {background} is {ratio:F2}, §11.1 records {recorded}"));
        Assert.True(ratio >= minimum, string.Create(CultureInfo.InvariantCulture, $"{foreground} on {background} is {ratio:F2}, below {minimum}"));
    }

    // The band fill is a wash of white at 1.8 % over the canvas: it must stay too faint to carry anything.
    [Fact]
    public void The_lane_fill_is_white_at_under_two_percent()
    {
        var fill = _tokens.Color("LaneFill");

        Assert.Equal(9, fill.Length);
        Assert.Equal("ffffff", fill[3..]);
        Assert.InRange(Convert.ToInt32(fill[1..3], 16) / 255.0, 0.017, 0.02);
        Assert.Contains("Color.LaneFill", _tokens.Resources["Brush.LaneFill"].Attribute("Color")?.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Primary_fails_AA_as_text_so_the_app_never_uses_it_as_text() =>
        Assert.InRange(_tokens.Contrast("Primary", "Canvas"), 4.1, TextAa - 0.001);

    [Fact]
    public void White_on_DESIGN_md_hover_fails_AA_so_the_hover_fill_is_the_darker_one() =>
        Assert.True(Wcag.Contrast(_tokens.Color("OnPrimary"), _tokens.Color("PrimaryHover")) < TextAa);

    [Fact]
    public void Hairlines_are_below_the_non_text_minimum_so_they_only_decorate()
    {
        Assert.True(_tokens.Contrast("Hairline", "Canvas") < NonText);
        Assert.True(_tokens.Contrast("HairlineStrong", "Canvas") < NonText);
    }

    // InkTertiary passes nothing, so it may colour only disabled text: a setter in a ':disabled' style, or a palette
    // entry whose name says Disabled.
    [Fact]
    public void Ink_tertiary_is_used_only_for_disabled_text()
    {
        var misuse = (
            from file in UiSource.Axaml.Where(f => Path.GetFileName(f) != "Tokens.axaml")
            from e in XDocument.Load(file).Descendants()
            from a in e.Attributes()
            where a.Value.Contains("InkTertiary", StringComparison.Ordinal) && !Disabled(e, a)
            select $"{Path.GetFileName(file)}: <{e.Name.LocalName} {a.Name.LocalName}=\"{a.Value}\">").ToList();

        Assert.True(misuse.Count == 0, "InkTertiary outside a disabled state:\n" + string.Join('\n', misuse));
    }

    // UI spec §14: no colour literal outside Theme/; styles and views bind the Brush.* tokens.
    [Fact]
    public void No_axaml_outside_Theme_holds_a_hex_colour()
    {
        var found = (
            from file in UiSource.Axaml
            where Path.GetFileName(Path.GetDirectoryName(file)) != "Theme"
            from line in File.ReadLines(file).Select((text, i) => (text, n: i + 1))
            where HexColour.IsMatch(line.text)
            select string.Create(CultureInfo.InvariantCulture, $"{Path.GetFileName(file)}:{line.n}: {line.text.Trim()}")).ToList();

        Assert.True(found.Count == 0, "hex colours outside Theme/ (use a Brush.* token):\n" + string.Join('\n', found));
    }

    // The #rrggbb a Brush.<name> resource paints: its Color is a {StaticResource Color.X} reference or a literal.
    private static string BrushColor(string name)
    {
        var value = _tokens.Resources["Brush." + name].Attribute("Color")?.Value.Trim() ?? string.Empty;
        const string Reference = "{StaticResource Color.";

        return value.StartsWith(Reference, StringComparison.Ordinal) ? _tokens.Color(value[Reference.Length..^1]) : value.ToLowerInvariant();
    }

    // A background row names a Color.* key, or a Brush.* resource when the fill has no colour key (Clickable.Pressed).
    private static string Background(string name) =>
        name.StartsWith("Brush.", StringComparison.Ordinal) ? BrushColor(name["Brush.".Length..]) : _tokens.Color(name);

    private static bool Disabled(XElement element, XAttribute attribute) =>
        attribute.Name.LocalName.Contains("Disabled", StringComparison.Ordinal)
        || element.AncestorsAndSelf().Any(a => a.Attribute("Selector")?.Value.Contains(":disabled", StringComparison.Ordinal) == true);

    [GeneratedRegex("#(?:[0-9a-fA-F]{8}|[0-9a-fA-F]{6}|[0-9a-fA-F]{3})\\b", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HexColour { get; }
}
