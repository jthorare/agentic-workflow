using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Setup;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Home;

/// <summary>Home's setup line and health strip (F3 home spec §1 Q5 and Q6, §8 slice 4): the line is a card with a 120 px meter between its count and Continue; the strip is five cells in one row.</summary>
[Lane(TestLane.Integration)]
public sealed class HomeStripTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task OnHome(double width, Action<ShellWindow> check)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(HomeStripTests).Assembly).Dispatch(async () =>
        {
            var records = new InMemoryEndpointRecords();
            records.Write(A.Record());
            var api = new FakeSindriApi { Catalog = RouteAnswer.Api("commands", "catalog"), SetupAnswer = SetupFixture.Answer("running-progress") };
            var shell = new ShellViewModel(records, api, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
            await shell.Restore(Token);
            var window = new ShellWindow { DataContext = shell, Width = width };
            try
            {
                window.Show();
                _ = shell.Run(stop.Token);
                for (var i = 0; i < 300 && !(shell.State == ShellState.Normal && shell.Setup.IsHomeLineShown && shell.Queue.Groups.Count > 0); i++)
                {
                    await Task.Delay(10, Token);
                    Dispatcher.UIThread.RunJobs();
                }

                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                check(window);
            }
            finally
            {
                await stop.CancelAsync();
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }

            return true;
        }, Token);
    }

    private static Rect Box(Window window, string name)
    {
        var control = window.GetVisualDescendants().OfType<Control>().Single(c => c.Name == name);
        return new Rect(control.TranslatePoint(default, window) ?? default, control.Bounds.Size);
    }

    [Fact]
    public async Task The_setup_line_has_a_120_px_meter_between_its_words_and_Continue()
    {
        await OnHome(1440, window =>
        {
            Rect line = Box(window, "SetupHomeLine"), meter = Box(window, "SetupHomeMeter"), link = Box(window, "SetupHomeLink"), go = Box(window, "SetupHomeContinue");

            Assert.True(Math.Abs(meter.Width - 120) < 1, string.Create(CultureInfo.InvariantCulture, $"the meter is 120 px wide, was {meter.Width}"));
            Assert.True(meter.Left >= link.Left && meter.Right <= go.Left + 0.5, "the meter sits before Continue");
            Assert.True(line.Contains(meter.TopLeft) && line.Contains(go.BottomRight - new Point(1, 1)), "the line holds the meter and Continue");
        });
    }

    [Fact]
    public async Task The_health_strip_is_health_cells_in_one_row_at_1440()
    {
        await OnHome(1440, window =>
        {
            var cells = window.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("cell")).ToList();

            // Five cells when the status carries an API version; the fixture's has none, so the API cell is left out (HealthViewModelTests).
            var served = ((ShellViewModel)window.DataContext!).Health.Detail.Strip.Count;
            Assert.Equal((true, served), (served >= 4, cells.Count));
            Assert.True(cells.TrueForAll(c => Math.Abs(c.TranslatePoint(default, window)!.Value.Y - cells[0].TranslatePoint(default, window)!.Value.Y) < 1), "the cells share one row");
        });
    }
}
