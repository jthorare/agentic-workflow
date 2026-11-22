using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The one key-hint row on the Context page (#3677, decision 4): drawn under the stage, one line, and the words the page says.</summary>
[Lane(TestLane.Integration)]
public sealed class ContextKeyHintsFramesTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_key_hint_row_is_one_visible_line_with_the_page_words_under_the_stage()
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ContextKeyHintsFramesTests).Assembly).Dispatch(async () =>
        {
            var records = new InMemoryEndpointRecords();
            records.Write(A.Record());
            var shell = new ShellViewModel(records, new FakeSindriApi { WholeMapAnswer = _ => new RouteAnswer(System.Net.HttpStatusCode.InternalServerError, "{}") }, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
            await shell.Restore(Token);
            var window = new ShellWindow { DataContext = shell };
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
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var page = Assert.Single(window.GetVisualDescendants().OfType<Control>(), c => c.Name == "ContextPage");
                var hints = page.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "ContextKeyHints");
                var stage = page.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "ContextStageHost");

                Assert.True(hints.IsEffectivelyVisible);
                Assert.Equal(shell.Context.KeyHints, hints.Text);
                Assert.True(Avalonia.VisualExtensions.TranslatePoint(hints, default, page)!.Value.Y >= Avalonia.VisualExtensions.TranslatePoint(stage, default, page)!.Value.Y + stage.Bounds.Height - 1, "the hint row sits under the stage");
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

    [Fact]
    public async Task The_key_hint_row_is_inside_the_window_not_scrolled_below_the_stage()
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ContextKeyHintsFramesTests).Assembly).Dispatch(async () =>
        {
            var records = new InMemoryEndpointRecords();
            records.Write(A.Record());
            var shell = new ShellViewModel(records, new FakeSindriApi { WholeMapAnswer = _ => new RouteAnswer(System.Net.HttpStatusCode.InternalServerError, "{}") }, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
            await shell.Restore(Token);
            var window = new ShellWindow { DataContext = shell, Width = 1280, Height = 800 };
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
                for (var pass = 0; pass < 4; pass++)
                {
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                }

                var page = Assert.Single(window.GetVisualDescendants().OfType<Control>(), c => c.Name == "ContextPage");
                var hints = page.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "ContextKeyHints");
                var scroller = page.FindAncestorOfType<ScrollViewer>()!;
                var bottom = Avalonia.VisualExtensions.TranslatePoint(hints, default, scroller)!.Value.Y + hints.Bounds.Height;

                // The stage takes the height the window leaves, so the hint row has to be left room under it: the real app drew it below the fold.
                Assert.True(bottom <= scroller.Viewport.Height + 0.5, $"the hint row ends at {bottom}, below the viewport's {scroller.Viewport.Height}");
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
}
