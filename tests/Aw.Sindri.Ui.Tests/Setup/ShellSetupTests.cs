using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Setup;

/// <summary>The setup checklist in the shell: read on connect and with every event, G U, the `setup` link and Home's line open Settings at the checklist, a row's button opens the approve-all dialog, and Home hides the setup rows while the line stands for them.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellSetupTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new()
    {
        Catalog = RouteAnswer.SpecShaped("commands", "extra-approve-all-ready"),
        NeedsYouView = RouteAnswer.Api("needs-you", "empty"),
        SetupAnswer = SetupFixture.Answer("extra-priority-mix"),
    };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Connected()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal && shell.Setup.Steps.Count > 0, Token);
        return shell;
    }

    [Fact]
    public Task Setup_is_read_on_connect_and_again_with_every_event() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var before = _server.SetupCalls;

        await shell.Health.RefreshViews(new Uri("http://127.0.0.1:7410/"), Token);

        Assert.True(before >= 1);
        Assert.True(_server.SetupCalls > before);
        Assert.Equal(["repo", "profile", "mcp", "map"], shell.Setup.Steps.Select(s => s.Id), StringComparer.Ordinal);
    }, Token);

    [Fact]
    public Task G_U_opens_Settings_at_the_checklist() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var reveals = 0;
        shell.Setup.RevealRequested += (_, _) => reveals++;

        _ = shell.PressKey("G");
        _ = shell.PressKey("U");

        Assert.Equal((PageId.Settings, true, 1), (shell.CurrentPage, shell.IsSettings, reveals));
    }, Token);

    [Fact]
    public Task The_setup_deep_link_opens_Settings_at_the_checklist() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var reveals = 0;
        shell.Setup.RevealRequested += (_, _) => reveals++;
        shell.GoTo(PageId.Settings);

        Assert.True(DeepLink.TryParse("setup", out var link));
        shell.Navigate(link);

        Assert.Equal((PageId.Settings, 2, "setup"), (shell.CurrentPage, reveals, link.ToString()));   // already on Settings: it still scrolls to the checklist
    }, Token);

    [Fact]
    public Task The_line_on_Home_opens_Settings_at_the_checklist() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var reveals = 0;
        shell.Setup.RevealRequested += (_, _) => reveals++;

        shell.Setup.RequestChecklist();

        Assert.Equal((PageId.Settings, 1), (shell.CurrentPage, reveals));
    }, Token);

    [Fact]
    public Task Continue_opens_Settings_at_the_first_open_step() => UiThread.Run(async () =>
    {
        _server.SetupAnswer = SetupFixture.Answer("profile-needs-approval");
        var shell = await Connected();
        string? at = null;
        shell.Setup.RevealRequested += (_, e) => at = e.StepId;

        shell.Setup.Continue();

        Assert.Equal((PageId.Settings, "sources"), (shell.CurrentPage, at));   // the accounts step is step 2 and the first one still to do
    }, Token);

    [Fact]
    public Task Home_hides_only_the_setup_row_the_line_names_and_shows_them_all_again_when_setup_is_finished() => UiThread.Run(async () =>
    {
        _server.NeedsYouView = RouteAnswer.SpecShaped("needs-you", "extra-setup-rows");
        var shell = await Connected();
        await ShellWait.Until(shell, () => shell.Setup.IsHomeLineShown, Token);
        var named = shell.Setup.HomeStepText;
        Assert.DoesNotContain(shell.NeedsYou.Rows, r => r.Title == named);   // the line stands for this one only
        Assert.NotEmpty(shell.NeedsYou.Rows);   // the other setup rows stay

        _server.SetupAnswer = RouteAnswer.Api("setup", "all-done");
        await shell.Health.RefreshViews(new Uri("http://127.0.0.1:7410/"), Token);
        await ShellWait.Until(shell, () => shell.NeedsYou.Rows.Count == 1, Token);

        Assert.Equal(["setup:mcp"], shell.NeedsYou.Rows.Select(r => r.Id).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }, Token);

    [Fact]
    public Task Home_keeps_the_other_setup_rows_while_the_line_names_the_repo_step() => UiThread.Run(async () =>
    {
        _server.NeedsYouView = RouteAnswer.SpecShaped("needs-you", "extra-setup-rows-with-repo");
        _server.SetupAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, SetupFixture.With("extra-priority-mix", "repo", "todo").ToJsonString());
        var shell = await Connected();
        await ShellWait.Until(shell, () => shell.Setup.IsHomeLineShown, Token);

        var ids = shell.NeedsYou.Rows.Select(r => r.Id).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(shell.Setup.Steps.Single(s => s.Id == "repo").Title, shell.Setup.HomeStepText);
        Assert.Equal(["setup:mcp"], ids, StringComparer.Ordinal);   // setup:repo is the line's, setup:recipes is a retired ghost, the rest stay
    }, Token);

}
