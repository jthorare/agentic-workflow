using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
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

/// <summary>The Context page's question box (#3677, decision 2): full width, focused when the page opens, and / focuses it again from anywhere on the page.</summary>
[Lane(TestLane.Integration)]
public sealed class ContextHeaderFramesTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task Run(Action<ShellWindow, Control, TextBox> check)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ContextHeaderFramesTests).Assembly).Dispatch(async () =>
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
                check(window, page, page.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "AnchorBox"));
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
    public async Task The_question_box_has_the_focus_when_the_page_opens_and_fills_the_row_up_to_the_view_switch() => await Run((window, page, anchor) =>
    {
        var views = page.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "ContextViewSwitch");
        var gap = Avalonia.VisualExtensions.TranslatePoint(views, default, page)!.Value.X - (Avalonia.VisualExtensions.TranslatePoint(anchor, default, page)!.Value.X + anchor.Bounds.Width);
        var width = anchor.Bounds.Width;

        Assert.Same(anchor, window.FocusManager?.GetFocusedElement());
        Assert.True(anchor.Bounds.Width >= 500, string.Create(CultureInfo.InvariantCulture, $"the question box is {width} px wide"));
        Assert.True(gap <= 40, string.Create(CultureInfo.InvariantCulture, $"{gap} px of empty row sit between the question box and the Map and Files switch"));
    });

    [Fact]
    public async Task A_slash_from_anywhere_on_the_page_focuses_the_question_box() => await Run((window, page, anchor) =>
    {
        var files = page.GetVisualDescendants().OfType<RadioButton>().Single(r => r.Name == "ViewFilesButton");
        _ = files.Focus(NavigationMethod.Tab);
        Dispatcher.UIThread.RunJobs();
        Assert.NotSame(anchor, window.FocusManager?.GetFocusedElement());

        window.KeyPress(Key.Oem2, RawInputModifiers.None, PhysicalKey.Slash, keySymbol: "/");
        Dispatcher.UIThread.RunJobs();

        Assert.Same(anchor, window.FocusManager?.GetFocusedElement());
        Assert.True(string.IsNullOrEmpty(anchor.Text), "the slash is a shortcut, not text in the box");
    });
}
