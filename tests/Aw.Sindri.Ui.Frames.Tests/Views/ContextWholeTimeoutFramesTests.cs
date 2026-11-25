using System.Globalization;
using System.Net;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>#4363: the failure pill of a timed-out whole-map read keeps its Retry button inside the window, at the widest and the narrowest width the Context frames use.</summary>
[Lane(TestLane.Integration)]
public sealed class ContextWholeTimeoutFramesTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task Run(double width, string frame)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var timedOut = RouteAnswer.Problem(HttpStatusCode.GatewayTimeout, "SND-UI-002", SindriUiErrors.DeadlineMessage("127.0.0.1:7410", SindriApiClient.CallTimeout.TotalSeconds));
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ContextWholeTimeoutFramesTests).Assembly).Dispatch(async () =>
        {
            var records = new InMemoryEndpointRecords();
            records.Write(A.Record());
            var shell = new ShellViewModel(records, new FakeSindriApi { WholeMapAnswer = _ => timedOut }, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
            await shell.Restore(Token);
            var window = new ShellWindow { DataContext = shell, Width = width, Height = 800 };
            try
            {
                window.Show();
                _ = shell.Run(stop.Token);
                for (var i = 0; i < 200 && shell.State != ShellState.Normal; i++)
                {
                    await Task.Delay(10, Token);
                    Dispatcher.UIThread.RunJobs();
                }

                shell.GoTo(PageId.Context);
                for (var i = 0; i < 200 && !shell.Context.Whole.IsFailed; i++)
                {
                    await Task.Delay(10, Token);
                    Dispatcher.UIThread.RunJobs();
                }

                Check(window, shell, frame);
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

    private static void Check(ShellWindow window, ShellViewModel shell, string frame)
    {
        window.UpdateLayout();
        FrameFile.Save(window, frame);
        var page = Assert.Single(window.GetVisualDescendants().OfType<Control>(), c => c.Name == "ContextPage");
        var retry = page.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "WholeRetry");
        var left = Avalonia.VisualExtensions.TranslatePoint(retry, default, page)!.Value.X;
        var right = left + retry.Bounds.Width;

        Assert.Equal(Labels.ContextWholeTimedOut, shell.Context.Whole.FailedText);
        Assert.True(retry.IsVisible);
        Assert.True(left >= 0 && right <= page.Bounds.Width, string.Create(CultureInfo.InvariantCulture, $"Retry spans {left}..{right} in a page {page.Bounds.Width} px wide"));
    }

    [Fact]
    public Task A_timed_out_read_keeps_retry_in_the_window_at_1280() => Run(1280, "context-whole-timeout-1280");

    [Fact]
    public Task A_timed_out_read_keeps_retry_in_the_window_at_1000() => Run(1000, "context-whole-timeout-narrow");
}
