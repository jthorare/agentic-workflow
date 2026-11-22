using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Theme;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The System page's Services card puts cadence and last run in their own columns: last run starts at one x on every row, clear of the cadence (#4000).</summary>
[Lane(TestLane.Integration)]
public sealed class ServicesCardColumnsTests : FrameHost
{
    private const double Tolerance = 0.5;

    [Theory]
    [InlineData(1000, 1.0)]
    [InlineData(1000, 1.3)]
    [InlineData(1280, 1.0)]
    [InlineData(1280, 1.3)]
    public Task Last_run_starts_at_one_x_on_every_row_and_never_touches_the_cadence(double width, double scale) => OnUi(async () =>
    {
        // The built-in services carry two-digit cadences ("every 15m", "every 24h", "every 30s") and both last-run texts.
        Server.ServiceView = RouteAnswer.Api("services", "ran-ok");
        var (window, shell) = await Connected(width: width);
        var resources = Application.Current!.Resources;
        TextScale.Apply(resources, scale);
        try
        {
            await Check(window, shell);
        }
        finally
        {
            TextScale.Apply(resources, 1.0);
        }
    });

    private static async Task Check(ShellWindow window, ShellViewModel shell)
    {
        var minGap = Assert.IsType<double>(window.FindResource("Space.XS"));
        shell.GoTo(PageId.System);
        Settle(window);

        var list = Named<ItemsControl>(window, "ServicesList");
        await Until(window, () => Rows(list).Count > 1, "the Services card never listed its services");

        var culture = CultureInfo.InvariantCulture;
        var rows = Rows(list);
        var starts = new List<double>();
        foreach (var row in rows)
        {
            var cadence = Assert.IsType<TextBlock>(row.Children[1]);
            var lastRun = Assert.IsType<TextBlock>(row.Children[2]);
            var cadenceRight = cadence.TranslatePoint(default, window)!.Value.X + cadence.TextLayout.Width;
            var lastRunLeft = lastRun.TranslatePoint(default, window)!.Value.X;
            Assert.True(lastRunLeft - cadenceRight >= minGap - Tolerance,
                string.Create(culture, $"'{cadence.Text}' ends at {cadenceRight}, '{lastRun.Text}' starts at {lastRunLeft}: under the {minGap} px gap"));
            starts.Add(lastRunLeft);
        }

        Assert.True(starts.Max() - starts.Min() <= Tolerance,
            string.Create(culture, $"last run starts anywhere from {starts.Min()} to {starts.Max()} px across {rows.Count} rows, not at one x"));
    }

    private static List<Grid> Rows(ItemsControl list) =>
        [.. list.GetVisualDescendants().OfType<Grid>().Where(g => g.Classes.Contains("data-row") && g.Children.Count == 3 && g.IsEffectivelyVisible)];
}
