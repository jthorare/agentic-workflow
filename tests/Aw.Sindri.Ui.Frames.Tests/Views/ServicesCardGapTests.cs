using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Theme;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// The System page's Services card (#4027): "last run" starts Space.SM (12 px) after the widest cadence where the card has room
/// (1280 px) and Space.XS (8 px) where it hasn't (1000 px), so the built-in 20-character names stay whole at 1000 px whatever the
/// clock shows; a longer name ends in an ellipsis. The row's one tip
/// holds the name on line 1 and what the service does under it (#4047).
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ServicesCardGapTests : FrameHost
{
    private const double Tolerance = 0.5;
    private const string LongName = "code-index-main-move-long-name";

    // The widest clock in Inter at body size, measured per digit from the font's advances: 0 is the widest tens of the hour,
    // 4 every other place ("last run 04:44:44" 104.1 px at 13 px, "last run 11:11:11" 90.4 px).
    private const string WidestClock = "last run 04:44:44";
    private const string NarrowClock = "last run 11:11:11";

    private static readonly string[] _twentyCharacterNames = ["code-index-main-move", "context-concept-scan", "toolkit-calls-expire"];

    [Theory]
    [InlineData(1000, 1.0)]
    [InlineData(1000, 1.3)]
    [InlineData(1280, 1.0)]
    [InlineData(1280, 1.3)]
    public Task Last_run_starts_a_small_space_after_the_widest_cadence_or_an_xs_one_at_1000(double width, double scale) => Scaled(width, scale, async (window, shell) =>
    {
        var gap = width < 1280 ? "Space.XS" : "Space.SM";
        foreach (var row in await Rows(window, shell))
        {
            AssertGap(window, row, gap);
        }
    });

    [Theory]
    [InlineData(1000)]
    [InlineData(1280)]
    public Task No_built_in_name_is_trimmed_at_text_scale_1_3(double width) => Scaled(width, 1.3, async (window, shell) =>
    {
        foreach (var row in await Rows(window, shell))
        {
            AssertWhole(row);
        }
    });

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.3)]
    public Task No_twenty_character_name_is_trimmed_at_1000_with_the_widest_clock_digits(double scale) => OnUi(async () =>
    {
        Server.ServiceView = Clocks();
        await WithScale(1000, scale, async (window, shell) =>
        {
            var rows = await Rows(window, shell);
            Assert.Contains(rows, r => string.Equals(LastRun(r).Text, WidestClock, StringComparison.Ordinal));
            foreach (var expected in _twentyCharacterNames)
            {
                AssertWhole(rows.Single(r => string.Equals(NameBlock(r).Text, expected, StringComparison.Ordinal)));
            }

            foreach (var row in rows)
            {
                AssertGap(window, row, "Space.XS");
            }
        });
    });

    [Fact]
    public Task Last_run_is_as_wide_whatever_digits_the_clock_shows() => OnUi(async () =>
    {
        Server.ServiceView = Clocks();
        await WithScale(1000, 1.0, async (window, shell) =>
        {
            var clocks = (await Rows(window, shell)).Select(LastRun).Where(t => t.Text?.StartsWith("last run ", StringComparison.Ordinal) == true).ToList();
            Assert.Contains(clocks, t => string.Equals(t.Text, NarrowClock, StringComparison.Ordinal));
            var widths = clocks.Select(t => t.TextLayout.Width).ToList();
            Assert.True(widths.Max() - widths.Min() < 0.01,
                string.Create(CultureInfo.InvariantCulture, $"last run is {widths.Min()} to {widths.Max()} px wide by the digits shown, so the name's room follows the clock"));
        });
    });

    [Fact]
    public Task The_row_tip_holds_the_name_on_line_one_and_what_the_service_does_under_it() => Scaled(1280, 1.0, async (window, shell) =>
    {
        foreach (var row in await Rows(window, shell))
        {
            var service = Assert.IsType<ServiceRowViewModel>(row.DataContext);
            var expected = service.What.Length > 0 ? service.Name + "\n" + service.What : service.Name;
            Assert.Equal(expected, ToolTip.GetTip(row));
            Assert.Null(ToolTip.GetTip(NameBlock(row)));
        }
    });

    [Fact]
    public Task A_thirty_character_name_ends_in_an_ellipsis_and_shows_in_full_on_hover() => OnUi(async () =>
    {
        var body = GoldenFiles.Node("services", "ran-ok");
        body["services"]![0]!["name"] = LongName;
        Server.ServiceView = new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());
        await WithScale(1000, 1.3, async (window, shell) =>
        {
            var rows = await Rows(window, shell);
            var row = rows.Single(r => string.Equals(NameBlock(r).Text, LongName, StringComparison.Ordinal));
            var name = NameBlock(row);
            Assert.Equal(TextTrimming.CharacterEllipsis, name.TextTrimming);
            Assert.True(name.TextLayout.TextLines.Any(l => l.HasCollapsed), "the 30-character name was not shortened");
            var tip = Assert.IsType<string>(ToolTip.GetTip(row));
            Assert.Equal(LongName, tip.Split('\n')[0]);
        });
    });

    private Task Scaled(double width, double scale, Func<ShellWindow, ShellViewModel, Task> check) => OnUi(async () =>
    {
        // The built-in services: names up to 20 characters, cadences up to "every 15m", both last-run texts.
        Server.ServiceView = RouteAnswer.Api("services", "ran-ok");
        await WithScale(width, scale, check);
    });

    private async Task WithScale(double width, double scale, Func<ShellWindow, ShellViewModel, Task> check)
    {
        var (window, shell) = await Connected(width: width);
        var resources = Application.Current!.Resources;
        TextScale.Apply(resources, scale);
        try
        {
            await check(window, shell);
        }
        finally
        {
            TextScale.Apply(resources, 1.0);
        }
    }

    private static async Task<List<Grid>> Rows(ShellWindow window, ShellViewModel shell)
    {
        shell.GoTo(PageId.System);
        Settle(window);
        var list = Named<ItemsControl>(window, "ServicesList");
        await Until(window, () => DataRows(list).Count > 1, "the Services card never listed its services");
        return DataRows(list);
    }

    private static List<Grid> DataRows(ItemsControl list) =>
        [.. list.GetVisualDescendants().OfType<Grid>().Where(g => g.Classes.Contains("data-row") && g.Children.Count == 3 && g.IsEffectivelyVisible)];

    // The built-in services, each with a last run: the widest clock and the narrowest in turn, in local time as the row shows it.
    private static RouteAnswer Clocks()
    {
        var body = GoldenFiles.Node("services", "ran-ok");
        var services = body["services"]!.AsArray();
        for (var i = 0; i < services.Count; i++)
        {
            var at = i % 2 == 0 ? LocalAt(4, 44, 44) : LocalAt(11, 11, 11);
            services[i]!["lastRun"] = new JsonObject { ["slot"] = at, ["outcome"] = "ok", ["code"] = null, ["ranAt"] = at };
        }

        return new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());
    }

    private static string LocalAt(int hour, int minute, int second) =>
        new DateTimeOffset(new DateTime(2026, 1, 1, hour, minute, second, DateTimeKind.Local)).ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static TextBlock NameBlock(Grid row) => Assert.IsType<TextBlock>(row.Children[0]);

    private static TextBlock LastRun(Grid row) => Assert.IsType<TextBlock>(row.Children[2]);

    // "last run" starts at least the named space after the end of the cadence's text.
    private static void AssertGap(ShellWindow window, Grid row, string space)
    {
        var minGap = Assert.IsType<double>(window.FindResource(space));
        var cadence = Assert.IsType<TextBlock>(row.Children[1]);
        var lastRun = LastRun(row);
        var cadenceRight = cadence.TranslatePoint(default, window)!.Value.X + cadence.TextLayout.Width;
        var lastRunLeft = lastRun.TranslatePoint(default, window)!.Value.X;
        Assert.True(lastRunLeft - cadenceRight >= minGap - Tolerance,
            string.Create(CultureInfo.InvariantCulture, $"'{cadence.Text}' ends at {cadenceRight}, '{lastRun.Text}' starts at {lastRunLeft}: {lastRunLeft - cadenceRight} px, under {minGap}"));
    }

    // The name is not shortened and its whole text fits its column.
    private static void AssertWhole(Grid row)
    {
        var name = NameBlock(row);
        var natural = NaturalWidth(name);
        var column = row.ColumnDefinitions[0].ActualWidth;
        Assert.False(name.TextLayout.TextLines.Any(l => l.HasCollapsed), name.Text + " is trimmed");
        Assert.True(natural <= column, string.Create(CultureInfo.InvariantCulture, $"'{name.Text}' needs {natural} px, its column is {column} px wide"));
    }

    // The width the whole name needs, untrimmed, in the block's own font.
    private static double NaturalWidth(TextBlock block)
    {
        using var layout = new TextLayout(block.Text, new Typeface(block.FontFamily, block.FontStyle, block.FontWeight), block.FontSize, foreground: null, letterSpacing: block.LetterSpacing);
        return layout.Width;
    }
}
