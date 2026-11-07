using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Linear;

/// <summary>Leaving an issue sheet with unsaved edits by any way into the shell (Linear hub spec §3): a link, Back, or Connect; and the Connect button reaching Settings.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellIssueSheetLeaveTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static DeepLink Link(string text) => DeepLink.TryParse(text, out var link) ? link : throw new InvalidOperationException(text);

    private async Task<ShellViewModel> Shell()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal, Token);
        return shell;
    }

    private static async Task Editing(ShellViewModel shell)
    {
        await ShellWait.Until(shell, () => shell.IssueSheet.CanEdit && shell.IssueSheet.EditFields[0].IsEnabled, Token);
        var priority = shell.IssueSheet.EditFields[1];
        priority.Selected = priority.Options.Single(o => o.Name == "High");
    }

    [Fact]
    public Task A_link_to_another_issue_with_unsaved_edits_asks_first_and_Discard_goes_on() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        shell.Navigate(Link("linear/ENG-12"));
        await Editing(shell);

        shell.Navigate(Link("linear/ENG-13"));
        var asked = (shell.IssueSheet.IsConfirmingDiscard, shell.SheetSubject, shell.IssueSheet.Key);
        shell.IssueSheet.KeepEditing();
        var kept = (shell.IssueSheet.IsConfirmingDiscard, shell.SheetSubject, shell.IssueSheet.HasChanges);
        shell.Navigate(Link("linear/ENG-13"));
        shell.IssueSheet.Discard();

        Assert.Equal((true, "ENG-12", "ENG-12"), asked);
        Assert.Equal((false, "ENG-12", true), kept);
        Assert.Equal(("ENG-13", "ENG-13"), (shell.SheetSubject, shell.IssueSheet.Key));
    }, Token);

    [Fact]
    public Task A_link_that_replaces_the_sheet_with_something_else_asks_first_too() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        shell.Navigate(Link("linear/ENG-12"));
        await Editing(shell);

        shell.Navigate(Link("item/ITM-1"));
        var asked = (shell.IssueSheet.IsConfirmingDiscard, shell.IsIssueSheet);
        shell.IssueSheet.Discard();

        Assert.Equal((true, true), asked);
        Assert.Equal((false, true), (shell.IsIssueSheet, shell.IsSheetOpen));
    }, Token);

    [Fact]
    public Task A_link_with_nothing_unsaved_goes_straight_there() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        shell.Navigate(Link("linear/ENG-12"));
        await ShellWait.Until(shell, () => shell.IssueSheet.CanEdit, Token);

        shell.Navigate(Link("linear/ENG-13"));

        Assert.Equal((false, "ENG-13"), (shell.IssueSheet.IsConfirmingDiscard, shell.SheetSubject));
    }, Token);

    [Fact]
    public Task Back_with_unsaved_edits_then_Discard_returns_along_the_trail_like_Back_with_none() => UiThread.Run(async () =>
    {
        var golden = GoldenFiles.Node("workflow-instance", "project-build");
        golden["open"]!["route"] = "linear/ENG-12";
        _server.InstanceBody = _ => new RouteAnswer(HttpStatusCode.OK, golden.ToJsonString());
        var shell = await Shell();
        shell.OpenInstance((string)golden["address"]!, (string)golden["id"]!);
        await ShellWait.Until(shell, () => shell.Instance.Phase == FramePhase.Shown, Token);
        shell.Instance.OpenServed();
        await Editing(shell);
        var onTheTrail = (shell.IsIssueSheet, shell.HasTrail);

        shell.Back();
        var asked = (shell.IssueSheet.IsConfirmingDiscard, shell.IsIssueSheet);
        shell.IssueSheet.Discard();

        Assert.Equal((true, true), onTheTrail);
        Assert.Equal((true, true), asked);
        Assert.Equal((true, false, (string)golden["address"]!), (shell.IsInstanceSheet, shell.HasTrail, shell.Instance.Address));
    }, Token);

    [Fact]
    public Task Connect_opens_Settings() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        shell.Navigate(Link("linear/ENG-12"));
        await ShellWait.Until(shell, () => shell.IssueSheet.Phase == FramePhase.Shown, Token);

        shell.IssueSheet.Connect();

        Assert.Equal(PageId.Settings, shell.CurrentPage);
    }, Token);
}
