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
    public static TheoryData<string, string, double, double> Pairs => new()
    {
        { "Ink", "Canvas", 19.6, TextAa },
        { "Ink", "Surface1", 17.9, TextAa },
        { "Ink", "Surface2", 17.2, TextAa },
        { "InkMuted", "Canvas", 14.3, TextAa },
        { "InkMuted", "Surface1", 13.0, TextAa },
        { "InkSubtle", "Canvas", 6.4, TextAa },
        { "InkSubtle", "Surface1", 5.9, TextAa },
        { "InkSubtle", "Surface2", 5.6, TextAa },
        { "InkTertiary", "Canvas", 3.6, 0 },
        { "InkTertiary", "Surface1", 3.3, 0 },
        { "PrimaryHover", "Canvas", 7.3, TextAa },
        { "PrimaryHover", "Surface1", 6.6, NonText },
        { "PrimaryHover", "Surface3", 6.1, NonText },   // the palette's selected-row bar on its card (U1 run 1, a11y-4)
        { "OnPrimary", "Primary", 4.70, TextAa },
        { "OnPrimary", "PrimaryHoverFill", 5.5, TextAa },
        { "OnPrimary", "PrimaryPressed", 6.2, TextAa },
        { "Error", "Canvas", 6.0, TextAa },
        { "Error", "Surface1", 5.5, TextAa },
        { "Success", "Canvas", 6.6, TextAa },
        { "Attention", "Canvas", 9.4, TextAa },
        { "Attention", "Surface1", 8.6, TextAa },
        { "Attention", "AttentionSoft", 7.9, TextAa },   // the attn chip
        { "Error", "ErrorSoft", 5.3, TextAa },   // the fail chip
        { "Success", "SuccessSoft", 5.8, TextAa },   // the ok chip
        { "InkMuted", "Surface2", 12.5, TextAa },   // the neutral chip
        { "Hairline", "Canvas", 1.3, 0 },
        { "HairlineStrong", "Canvas", 1.7, 0 },
        // The context map (F3 spec 1 §2 "Tokens"): the wire and its ports are decoration, 1.3+ like the hairline; the
        // highlighted wire and the anchor ring are the same values as InkSubtle and InkMuted, so they pass as non-text.
        { "Wire", "Canvas", 2.4, 1.3 },
        { "Wire", "Surface1", 2.2, 1.3 },
        { "WireHi", "Canvas", 6.4, NonText },
        { "WireHi", "Surface1", 5.9, NonText },
        { "RingAnchor", "Canvas", 14.3, NonText },
        { "RingAnchor", "Surface1", 13.0, NonText },
    };

    [Theory]
    [MemberData(nameof(Pairs))]
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
        Assert.InRange(_tokens.Contrast("Primary", "Canvas"), 4.4, TextAa - 0.001);

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

    private static bool Disabled(XElement element, XAttribute attribute) =>
        attribute.Name.LocalName.Contains("Disabled", StringComparison.Ordinal)
        || element.AncestorsAndSelf().Any(a => a.Attribute("Selector")?.Value.Contains(":disabled", StringComparison.Ordinal) == true);

    [GeneratedRegex("#(?:[0-9a-fA-F]{8}|[0-9a-fA-F]{6}|[0-9a-fA-F]{3})\\b", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HexColour { get; }
}
