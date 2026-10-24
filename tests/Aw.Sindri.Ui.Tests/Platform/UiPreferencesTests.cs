using System.Text;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Tests.Platform;

/// <summary>UI spec §12.3: the preferences file's shape, its clamp on read and its reset when malformed.</summary>
[Lane(TestLane.Unit)]
public sealed class UiPreferencesTests
{
    [Fact]
    public void The_defaults_are_version_1_on_Home_at_scale_1_with_the_default_sheet()
    {
        var d = UiPreferences.Defaults;

        Assert.Equal((1, null, 520.0, "Home", 1.0, false), (d.Version, d.Window, d.SheetWidth, d.LastPage, d.TextScale, d.ReduceMotion));
        Assert.Empty(d.SeenIds);
        Assert.Empty(d.PaletteHistory);
    }

    [Fact]
    public void The_file_is_camel_case_json_with_a_version()
    {
        var json = Encoding.UTF8.GetString(PreferencesJson.Write(UiPreferences.Defaults with { Window = new WindowBounds(1, 2, 1000, 700), SeenIds = ["a"] }));

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.Equal(1000, root.GetProperty("window").GetProperty("width").GetDouble());
        Assert.Equal(520, root.GetProperty("sheetWidth").GetDouble());
        Assert.Equal("Home", root.GetProperty("lastPage").GetString());
        Assert.Equal("a", root.GetProperty("seenIds")[0].GetString());
        Assert.Equal(0, root.GetProperty("paletteHistory").GetArrayLength());
        Assert.Equal(1, root.GetProperty("textScale").GetDouble());
        Assert.False(root.GetProperty("reduceMotion").GetBoolean());
    }

    [Fact]
    public void A_file_round_trips()
    {
        var prefs = UiPreferences.Defaults with { Window = new WindowBounds(-10, 20, 1440, 900), SheetWidth = 700, LastPage = "Settings", SeenIds = ["r1", "r2"], PaletteHistory = ["Health"], TextScale = 0.9, ReduceMotion = true };

        Assert.Equal(prefs, PreferencesJson.Read(PreferencesJson.Write(prefs)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{ not json")]
    [InlineData("""{"version":"one"}""")]
    [InlineData("""{"version":0,"sheetWidth":600,"lastPage":"Work","seenIds":[],"paletteHistory":[],"textScale":1,"reduceMotion":false}""")]
    [InlineData("""{"version":2,"sheetWidth":600,"lastPage":"Work","seenIds":[],"paletteHistory":[],"textScale":1,"reduceMotion":false}""")]
    public void A_malformed_or_foreign_file_reads_as_the_defaults(string text) =>
        Assert.Equal(UiPreferences.Defaults, PreferencesJson.Read(Encoding.UTF8.GetBytes(text)));

    [Fact]
    public void Missing_lists_read_as_empty() =>
        Assert.Equal(
            UiPreferences.Defaults with { LastPage = "Work" },
            PreferencesJson.Read("""{"version":1,"sheetWidth":520,"lastPage":"Work","textScale":1,"reduceMotion":false}"""u8));

    [Theory]
    [InlineData(300, 200, 900, 600)]
    [InlineData(899, 599, 900, 600)]
    [InlineData(1280, 800, 1280, 800)]
    public void The_window_grows_to_the_minimum(double width, double height, double clampedWidth, double clampedHeight) =>
        Assert.Equal(new WindowBounds(5, 6, clampedWidth, clampedHeight), (UiPreferences.Defaults with { Window = new WindowBounds(5, 6, width, height) }).Clamp().Window);

    [Theory]
    [InlineData(double.NaN, 0, 1000, 700)]
    [InlineData(0, double.PositiveInfinity, 1000, 700)]
    [InlineData(0, 0, double.NaN, 700)]
    [InlineData(0, 0, 1000, double.NegativeInfinity)]
    public void Bounds_that_arent_finite_are_dropped(double x, double y, double width, double height) =>
        Assert.Null((UiPreferences.Defaults with { Window = new WindowBounds(x, y, width, height) }).Clamp().Window);

    [Theory]
    [InlineData(100, 420)]
    [InlineData(420, 420)]
    [InlineData(600, 600)]
    [InlineData(760, 760)]
    [InlineData(5000, 760)]
    [InlineData(double.NaN, 520)]
    public void The_sheet_width_is_clamped(double width, double clamped) =>
        Assert.Equal(clamped, (UiPreferences.Defaults with { SheetWidth = width }).Clamp().SheetWidth);

    [Theory]
    [InlineData(0.5, 0.9)]
    [InlineData(1.1, 1.1)]
    [InlineData(2, 1.3)]
    [InlineData(double.NaN, 1)]
    [InlineData(double.PositiveInfinity, 1)]
    public void The_text_scale_is_clamped(double scale, double clamped) =>
        Assert.Equal(clamped, (UiPreferences.Defaults with { TextScale = scale }).Clamp().TextScale);

    [Theory]
    [InlineData("Home", "Home")]
    [InlineData("Settings", "Settings")]
    [InlineData("queue", "Home")]
    [InlineData("Decisions", "Home")]
    [InlineData("Health", "Home")]
    [InlineData("", "Home")]
    public void An_unknown_page_reads_as_Home(string page, string clamped) =>
        Assert.Equal(clamped, (UiPreferences.Defaults with { LastPage = page }).Clamp().LastPage);

    [Fact]
    public void Four_thousand_seen_ids_are_all_kept()
    {
        var many = Enumerable.Range(0, 4000).Select(i => "r" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();

        Assert.Equal(many, (UiPreferences.Defaults with { SeenIds = many }).Clamp().SeenIds);
    }

    [Fact]
    public void Seen_ids_drop_blanks_and_repeats_and_keep_the_newest()
    {
        var many = Enumerable.Range(0, UiPreferences.MaxSeenIds + 10).Select(i => "r" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();

        var clamped = (UiPreferences.Defaults with { SeenIds = ["", " ", .. many, "r5"] }).Clamp().SeenIds;

        Assert.Equal(UiPreferences.MaxSeenIds, clamped.Count);
        Assert.Equal(("r509", "r5"), (clamped[^2], clamped[^1]));   // a repeat keeps its newest place
        Assert.DoesNotContain("", clamped, StringComparer.Ordinal);
        Assert.Equal(clamped.Count, clamped.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Palette_history_keeps_the_newest_entries()
    {
        var many = Enumerable.Range(0, 30).Select(i => "cmd" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();

        var clamped = (UiPreferences.Defaults with { PaletteHistory = many }).Clamp().PaletteHistory;

        Assert.Equal(many.TakeLast(UiPreferences.MaxPaletteHistory), clamped, StringComparer.Ordinal);
    }

    [Fact]
    public void Clamping_fixes_the_version() => Assert.Equal(1, (UiPreferences.Defaults with { Version = 7 }).Clamp().Version);
}
