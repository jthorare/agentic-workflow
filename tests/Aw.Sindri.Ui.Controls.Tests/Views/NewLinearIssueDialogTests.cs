using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
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

namespace Aw.Sindri.Ui.Controls.Tests.Views;

/// <summary>
/// The Linear "New issue" dialog (Linear hub spec §3): `C` on the Linear view opens it with focus on the team, Enter in the title creates, Esc closes it with the
/// draft kept and focus back on the opener, and its polite line is in the tree whether or not the dialog is shown.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class NewLinearIssueDialogTests : IDisposable
{
    private static readonly string[] _fields = ["TeamField", "TitleField", "DescriptionField", "StateField", "AssigneeField", "PriorityField", "CycleField"];
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(NewLinearIssueDialogTests).Assembly)
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

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Connected()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        await Until(window, () => shell.State == ShellState.Normal && shell.Queue.Total > 0 && shell.Queue.Groups.Count > 0 && shell.Health.Catalog.Controls.Count > 0);
        shell.GoTo(PageId.Work);
        shell.Linear.Choose(linear: true);
        Settle(window);
        return (window, shell);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task Until(Window window, Func<bool> condition, string what = "the view never reached the expected state")
    {
        for (var i = 0; i < 300 && !condition(); i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        Settle(window);
        Assert.True(condition(), what);
    }

    private static T Named<T>(Visual within, string name)
        where T : Control => within.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static Control? Focused(Window window) => window.FocusManager?.GetFocusedElement() as Control;

    // `C` from the "New issue" button: the dialog opens, with focus on its team.
    private static async Task<Button> OpenFromButton(Window window, ShellViewModel shell)
    {
        var opener = Named<Button>(window, "CreateButton");
        Assert.True(opener.Focus(NavigationMethod.Tab));
        window.KeyPressQwerty(PhysicalKey.C, RawInputModifiers.None);
        await Until(window, () => shell.NewIssue.IsOpen && Focused(window) == Named<ComboBox>(window, "TeamField"));
        return opener;
    }

    [Fact]
    public Task The_dialog_is_named_New_issue_and_every_field_has_a_name() => OnUi(async () =>
    {
        var (window, shell) = await Connected();

        _ = await OpenFromButton(window, shell);

        var card = Named<Border>(window, "NewIssueCard");
        var names = _fields
            .Select(n => AutomationProperties.GetName(Named<Control>(window, n)));
        Assert.Equal(
            ("New issue", AutomationControlType.Window, "Team,Title,Description,State,Assignee,Priority,Cycle"),
            (AutomationProperties.GetName(card), AutomationProperties.GetControlTypeOverride(card), string.Join(',', names)));
    });

    [Fact]
    public Task Enter_in_the_title_creates_the_issue() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        _ = await OpenFromButton(window, shell);
        await Until(window, () => shell.NewIssue.TeamChoices.Count == 2);
        shell.NewIssue.Team = shell.NewIssue.TeamChoices[0];
        await shell.NewIssue.Reading;
        var title = Named<TextBox>(window, "TitleField");
        Assert.True(title.Focus(NavigationMethod.Tab));
        title.Text = "Document the retry budget";

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Until(window, () => _server.LinearCreates.Count == 1 && !shell.NewIssue.IsOpen);

        Assert.Equal(("ENG", "Document the retry budget"), (_server.LinearCreates[0].Team, _server.LinearCreates[0].Title));
    });

    [Fact]
    public Task Escape_closes_the_dialog_keeps_the_draft_and_returns_focus_to_the_opener() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        var opener = await OpenFromButton(window, shell);
        shell.NewIssue.Title = "Document the retry budget";

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Settle(window);

        Assert.Equal((false, "Document the retry budget", opener), (shell.NewIssue.IsOpen, shell.NewIssue.Title, Focused(window)));
    });

    [Fact]
    public Task The_polite_line_is_in_the_tree_before_the_dialog_opens_and_after_it_closes() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        var before = window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Name == "NewIssueLive");

        _ = await OpenFromButton(window, shell);
        shell.NewIssue.Close();
        Settle(window);

        Assert.Equal((true, true), (before, window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Name == "NewIssueLive")));
    });
}
