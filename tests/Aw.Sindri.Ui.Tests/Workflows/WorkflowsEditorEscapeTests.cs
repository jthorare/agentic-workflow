using System.Net;
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

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>Escape in the workflow editor sheet (#2258 PR 17 part 3a), through the real shell window: with the editor open it closes the editor and focus goes to Step settings (Edit is hidden while edit mode is on, #2807), and the shell's own sheet stays open; with the editor closed Escape reaches the shell as before.</summary>
[Lane(TestLane.Integration)]
public sealed class WorkflowsEditorEscapeTests : IDisposable
{
#pragma warning disable MA0136 // a raw string with line ends is the readable form of this JSON
    private const string Blocks = """
        {"groups":[
          {"group":"Triggers","blocks":[{"type":"manual","title":"Manual","isTrigger":true,"config":[],"outputs":{},"branches":["out"],"exampleConfig":null,"exampleSpec":null}]},
          {"group":"AI","blocks":[{"type":"run_agent","title":"Run an agent","isTrigger":false,
            "config":[{"name":"prompt","type":"text","required":true}],
            "outputs":{"text":"text"},"branches":["out"],"exampleConfig":null,"exampleSpec":null}]}
        ]}
        """;
#pragma warning restore MA0136

    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(WorkflowsEditorEscapeTests).Assembly)
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

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10, Token);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(condition());
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static T Named<T>(Window window, string name)
        where T : Control => window.GetVisualDescendants().OfType<T>().First(c => c.Name == name && c.IsEffectivelyVisible);

    // The workflow open on its runs with its node "agent" selected, and the shell's item sheet open over the page.
    private async Task<(ShellWindow Window, ShellViewModel Shell)> WithSheet()
    {
        _records.Write(A.Record());
        _f.Server.Catalog = RouteAnswer.Api("commands", "catalog");
        _f.Server.NeedsYouView = RouteAnswer.Api("needs-you", "empty");
        _f.Server.WorkflowBlocksAnswer = new RouteAnswer(HttpStatusCode.OK, Blocks);
        var shell = new ShellViewModel(_records, _f.Server, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell, Width = 1400, Height = 900 };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        await Until(() => shell.State == ShellState.Normal);
        shell.GoTo(PageId.Workflows);
        await Until(() => shell.Workflows.Rows.Count == 2);
        await shell.Workflows.OpenWorkflow(WorkflowsFixture.Slug, Token);
        Assert.True(shell.Workflows.Runs.Graph!.SelectNode("agent"));
        Settle(window);
        Assert.True(DeepLink.TryParse("item/ITM-01JC8", out var link));
        shell.Navigate(link);
        Settle(window);
        Assert.True(shell.IsSheetOpen);
        return (window, shell);
    }

    private static void PressEscape(Window window)
    {
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, keySymbol: null);
        Settle(window);
    }

    [Fact]
    public Task Escape_in_the_editor_closes_it_and_focuses_Step_settings_without_the_shell_closing_its_sheet() => OnUi(async () =>
    {
        var (window, shell) = await WithSheet();
        await shell.Workflows.OpenSettings(Token);
        Settle(window);
        Assert.True(shell.Workflows.IsSettingsOpen);
        Assert.True(Named<TextBox>(window, "EditorFieldText").Focus(NavigationMethod.Tab));

        PressEscape(window);

        Assert.False(shell.Workflows.IsSettingsOpen);
        Assert.True(shell.IsSheetOpen, "the shell's Escape acted too");
        Assert.False(shell.Workflows.ShowsEdit);   // edit mode stays on after the sheet closes, so Edit stays hidden: focus goes to Step settings
        Assert.Same(Named<Button>(window, "StepSettings"), window.FocusManager?.GetFocusedElement());
    });

    [Fact]
    public Task Escape_with_the_editor_closed_still_reaches_the_shell() => OnUi(async () =>
    {
        var (window, shell) = await WithSheet();
        Assert.False(shell.Workflows.IsSettingsOpen);
        Assert.True(Named<Button>(window, "WorkflowEdit").Focus(NavigationMethod.Tab));

        PressEscape(window);

        Assert.False(shell.IsSheetOpen);
    });
}
