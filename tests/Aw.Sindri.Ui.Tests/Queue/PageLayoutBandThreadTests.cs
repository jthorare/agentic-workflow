using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;

namespace Aw.Sindri.Ui.Tests.Queue;

/// <summary>The band tests must not build Avalonia controls on a thread other than the headless session's, because Avalonia makes the first dispatcher created after the session's per-test reset the UI thread, and then the session's own setup throws "a different thread owns it" (#1640).</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class PageLayoutBandThreadTests
{
    private static readonly TimeSpan _window = TimeSpan.FromSeconds(5);

    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(PageLayoutBandThreadTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    [Fact]
    public async Task The_band_tests_never_take_the_ui_thread_from_the_headless_session()
    {
        var clock = Stopwatch.StartNew();
        var bandLoop = Task.Run(async () =>
        {
            while (clock.Elapsed < _window)
            {
                await Invoke(nameof(PageLayoutBandTests.The_compact_band_keeps_title_status_and_age));
                await Invoke(nameof(PageLayoutBandTests.The_columns_beside_the_title_leave_it_160_px_at_the_start_of_each_tier), "Narrow", 760d);
            }
        }, TestContext.Current.CancellationToken);

        var dispatched = 0;
        var failed = 0;
        string? firstMessage = null;
        while (clock.Elapsed < _window)
        {
            dispatched++;
            try
            {
                _ = await OnUi(() =>
                {
                    var window = new Window { Content = new Border(), Width = 200, Height = 100 };
                    window.Show();
                    Dispatcher.UIThread.RunJobs();
                    window.Close();
                    return true;
                });
            }
            catch (InvalidOperationException e)
            {
                failed++;
                firstMessage ??= e.Message;
            }
        }

        await bandLoop;

        Assert.True(dispatched >= 20, string.Create(CultureInfo.InvariantCulture, $"only {dispatched} dispatches ran"));
        Assert.True(failed == 0, string.Create(CultureInfo.InvariantCulture, $"{failed} of {dispatched} dispatches failed; first: {firstMessage}"));
    }

    private static async Task Invoke(string method, params object[] args)
    {
        var instance = new PageLayoutBandTests();
        var info = typeof(PageLayoutBandTests).GetMethod(method)!;
        try
        {
            if (info.Invoke(instance, args) is Task task)
            {
                await task;
            }
        }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();
        }
    }
}
