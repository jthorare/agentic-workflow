using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Context.FileMap;
using Aw.Sindri.Ui.Views.Context.Node;
using Aw.Sindri.Ui.Views.Context.Review;
using Aw.Sindri.Ui.Views.Context.Sources;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The Context page in the shell window (U9a0): the real page replaces the placeholder, with its anchor box named and its seam hosts.</summary>
[Lane(TestLane.Integration)]
public sealed class ContextPageWindowTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Context_page_replaces_the_placeholder_in_the_window()
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ContextPageWindowTests).Assembly).Dispatch(async () =>
        {
            var records = new InMemoryEndpointRecords();
            records.Write(A.Record());
            var shell = new ShellViewModel(records, new FakeSindriApi { WholeMapAnswer = _ => new RouteAnswer(System.Net.HttpStatusCode.InternalServerError, "{}") }, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());   // no whole map: a query draws its neighbourhood
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
                Assert.Equal("ContextPage", page.GetType().Name);
                Assert.Same(shell.Context, page.DataContext);
                var anchor = page.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "AnchorBox");
                Assert.Equal(Labels.ContextAnchor, AutomationProperties.GetName(anchor));
                Assert.Same(anchor, window.FocusManager?.GetFocusedElement());
                AssertHosts(shell, page);
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
    public async Task Typing_an_anchor_draws_the_map_and_every_node_band_and_label_reaches_the_automation_tree()
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ContextPageWindowTests).Assembly).Dispatch(async () =>
        {
            var records = new InMemoryEndpointRecords();
            records.Write(A.Record());
            var shell = new ShellViewModel(records, new FakeSindriApi { WholeMapAnswer = _ => new RouteAnswer(System.Net.HttpStatusCode.InternalServerError, "{}") }, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());   // no whole map: the answer draws its neighbourhood, whose nodes are the ones heard
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
                shell.Context.AnchorText = (string)GoldenFiles.Node("context-lenses", "map-tabs")["anchor"]!["key"]!;
                await shell.Context.Submit(Token);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var heard = Heard.Tree(window).Select(n => n.Name).ToList();
                var map = shell.Context.Map!;
                var expected = map.Nodes.Select(n => n.Name).Concat(map.Bands.Select(b => b.Name)).Concat(map.Pills.Where(p => p.Always).Select(p => p.Text));
                Assert.All(expected, text => Assert.Contains(heard, h => string.Equals(h, text, StringComparison.Ordinal)));
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

    // Each stage's host is its seam view, bound to that stage's view model (a Panel, or a lost binding, fails here).
    private static void AssertSeams(ShellViewModel shell, Control page)
    {
        Control Host(string name) => page.GetVisualDescendants().OfType<Control>().Single(c => c.Name == name);
        Assert.Same(shell.Context.FileMap, Assert.IsType<FileMapTree>(Host("FileMapHost")).DataContext);
        Assert.Same(shell.Context.Review, Assert.IsType<MapReviewPanel>(Host("ReviewHost")).DataContext);
        Assert.Same(shell.Context.Node, Assert.IsType<NodeCard>(Host("NodeHost")).DataContext);
        Assert.Same(shell.Context.Sources, Assert.IsType<SourcesPanel>(Host("SourcesHost")).DataContext);
    }

    private static void AssertHosts(ShellViewModel shell, Control page)
    {
        AssertSeams(shell, page);
        Assert.Equal(shell.Context.HasHead, page.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "ContextHead").IsEffectivelyVisible);   // the head shows once the whole map is read (and not before)
        string[] hosts = ["LensStackHost", "FileMapHost", "ReviewHost", "NodeHost", "SourcesHost"];
        Assert.All(hosts, h => Assert.Contains(page.GetVisualDescendants().OfType<Control>(), c => c.Name == h));
    }
}
