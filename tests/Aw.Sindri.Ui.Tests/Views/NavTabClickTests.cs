using Avalonia;
using Avalonia.Automation;
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

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>Clicking a tab of the top bar's navigation (UI spec §3.2): each of the seven shows exactly its page, and an unbuilt page its arrives note.</summary>
[Lane(TestLane.Integration)]
public sealed class NavTabClickTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly InMemoryPreferencesStore _preferences = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(NavTabClickTests).Assembly)
            .Dispatch(async () =>
            {
                try
                {
                    await body();
                }
                finally
                {
                    await _stop.CancelAsync();
                    _windows.ForEach(w => w.Close());
                    Dispatcher.UIThread.RunJobs();
                }

                return true;
            }, Token);

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Show(ShellState state)
    {
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(_preferences), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        for (var i = 0; i < 200 && shell.State != state; i++)
        {
            await Task.Delay(10, Token);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(state, shell.State);
        Dispatcher.UIThread.RunJobs();
        return (window, shell);
    }

    [Theory]
    [InlineData(PageId.Home)]
    [InlineData(PageId.Workflows)]
    [InlineData(PageId.Work)]
    [InlineData(PageId.Recipes)]
    [InlineData(PageId.Context)]
    [InlineData(PageId.Agents)]
    [InlineData(PageId.System)]
    internal Task Clicking_a_tab_shows_exactly_its_page_and_its_arrives_note(PageId target) => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(ShellState.Normal);
        shell.GoTo(target == PageId.Work ? PageId.Recipes : PageId.Work);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        var tab = window.GetVisualDescendants().OfType<ListBoxItem>().Single(i => string.Equals(AutomationProperties.GetName(i), Labels.Page(target), StringComparison.Ordinal));
        var centre = tab.TranslatePoint(new Point(tab.Bounds.Width / 2, tab.Bounds.Height / 2), window)!.Value;

        window.MouseDown(centre, MouseButton.Left);
        window.MouseUp(centre, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        Assert.Equal(target, shell.CurrentPage);
        var shown = _pages
            .Where(n => window.GetVisualDescendants().OfType<Control>().First(c => c.Name == n).IsEffectivelyVisible).ToList();
        Assert.Equal([Own(target)], shown);
        if (target == PageId.Context)   // a pointer opening (not an arrow) puts focus in the anchor box
        {
            Assert.Equal("AnchorBox", (window.FocusManager?.GetFocusedElement() as Control)?.Name);
        }

        if (target is not (PageId.Work or PageId.Workflows or PageId.Context or PageId.Agents) && Labels.ArrivesWith(target) is { } arrives)   // Work is the queue (U2), Workflows the list (U8) and Context the page (U9), Agents the sessions (U11a), not placeholders
        {
            var page = window.GetVisualDescendants().OfType<PlaceholderPage>().Single(p => p.Name == Own(target));
            Assert.Equal(arrives, page.Note);
            Assert.Contains(page.GetVisualDescendants().OfType<TextBlock>(), t => string.Equals(t.Text, arrives, StringComparison.Ordinal));
        }
    });

    private static readonly string[] _pages = ["HomePage", "WorkflowsPage", "QueuePage", "RecipesPage", "ContextPage", "AgentsPage", "HealthPage"];

    // Re-check N1: the System page's region is named "System", so its heading says the same word.
    [Fact]
    public Task The_System_page_heading_is_the_name_of_its_region() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(ShellState.Normal);
        shell.GoTo(PageId.System);
        Dispatcher.UIThread.RunJobs();

        var title = window.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible && t.Classes.Contains("page-title"));

        Assert.Equal(shell.PageName, title.Text);
    });

    private static string Own(PageId page) => page switch
    {
        PageId.Home => "HomePage",
        PageId.Workflows => "WorkflowsPage",
        PageId.Work => "QueuePage",
        PageId.Recipes => "RecipesPage",
        PageId.Context => "ContextPage",
        PageId.Agents => "AgentsPage",
        PageId.System => "HealthPage",
        PageId.Jobs => "JobsPage",
        PageId.Settings => throw new ArgumentOutOfRangeException(nameof(page), page, message: null),
        _ => throw new ArgumentOutOfRangeException(nameof(page), page, message: null),
    };
}
