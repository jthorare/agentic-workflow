using System.Globalization;
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
/// The System page's Services card (#4027): "last run" starts at least Space.SM (12 px) after the widest cadence, the built-in
/// 20-character names stay whole at 1000 px, and a longer name ends in an ellipsis with its full text on hover.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ServicesCardGapTests : FrameHost
{
    private const double Tolerance = 0.5;
    private const string LongName = "code-index-main-move-long-name";

    [Theory]
    [InlineData(1000, 1.0)]
    [InlineData(1000, 1.3)]
    [InlineData(1280, 1.0)]
    [InlineData(1280, 1.3)]
    public Task Last_run_starts_at_least_a_small_space_after_the_widest_cadence(double width, double scale) => Scaled(width, scale, async (window, shell) =>
    {
        var minGap = Assert.IsType<double>(window.FindResource("Space.SM"));
        var rows = await Rows(window, shell);
        var culture = CultureInfo.InvariantCulture;
        foreach (var row in rows)
        {
            var cadence = Assert.IsType<TextBlock>(row.Children[1]);
            var lastRun = Assert.IsType<TextBlock>(row.Children[2]);
            var cadenceRight = cadence.TranslatePoint(default, window)!.Value.X + cadence.TextLayout.Width;
            var lastRunLeft = lastRun.TranslatePoint(default, window)!.Value.X;
            Assert.True(lastRunLeft - cadenceRight >= minGap - Tolerance,
                string.Create(culture, $"'{cadence.Text}' ends at {cadenceRight}, '{lastRun.Text}' starts at {lastRunLeft}: {lastRunLeft - cadenceRight} px, under {minGap}"));
        }
    });

    [Theory]
    [InlineData(1000)]
    [InlineData(1280)]
    public Task No_built_in_name_is_trimmed_at_text_scale_1_3(double width) => Scaled(width, 1.3, async (window, shell) =>
    {
        var culture = CultureInfo.InvariantCulture;
        foreach (var row in await Rows(window, shell))
        {
            var name = Assert.IsType<TextBlock>(row.Children[0]);
            var natural = NaturalWidth(name);
            var column = row.ColumnDefinitions[0].ActualWidth;
            Assert.False(name.TextLayout.TextLines.Any(l => l.HasCollapsed), name.Text + " is trimmed");
            Assert.True(natural <= column, string.Create(culture, $"'{name.Text}' needs {natural} px, its column is {column} px wide"));
        }
    });

    [Fact]
    public Task A_thirty_character_name_ends_in_an_ellipsis_and_shows_in_full_on_hover() => OnUi(async () =>
    {
        var body = GoldenFiles.Node("services", "ran-ok");
        body["services"]![0]!["name"] = LongName;
        Server.ServiceView = new RouteAnswer(System.Net.HttpStatusCode.OK, body.ToJsonString());
        await WithScale(1000, 1.3, async (window, shell) =>
        {
            var rows = await Rows(window, shell);
            var name = rows.Select(r => Assert.IsType<TextBlock>(r.Children[0])).Single(t => string.Equals(t.Text, LongName, StringComparison.Ordinal));
            Assert.Equal(TextTrimming.CharacterEllipsis, name.TextTrimming);
            Assert.True(name.TextLayout.TextLines.Any(l => l.HasCollapsed), "the 30-character name was not shortened");
            Assert.Equal(LongName, ToolTip.GetTip(name));
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

    // The width the whole name needs, untrimmed, in the block's own font.
    private static double NaturalWidth(TextBlock block)
    {
        using var layout = new TextLayout(block.Text, new Typeface(block.FontFamily, block.FontStyle, block.FontWeight), block.FontSize, foreground: null, letterSpacing: block.LetterSpacing);
        return layout.Width;
    }
}
