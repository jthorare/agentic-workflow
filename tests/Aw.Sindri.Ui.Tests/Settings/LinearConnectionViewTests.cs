using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Linear;
using Aw.Sindri.Ui.ViewModels.Settings;
using Aw.Sindri.Ui.Views;

namespace Aw.Sindri.Ui.Tests.Settings;

/// <summary>Settings → Connections → Linear under the headless platform: its headings and screen-reader names, the live region, what an agent session sees, and Connect from the issue sheet.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class LinearConnectionViewTests : FrameHost
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static T Find<T>(Visual within, string name)
        where T : Control => within.GetVisualDescendants().OfType<T>().Single(c => string.Equals(c.Name, name, StringComparison.Ordinal));

    // False until the box is in the tree: the Settings page draws it after Connect switches to it.
    private static bool Focused(Visual within, string name) =>
        within.GetVisualDescendants().OfType<TextBox>().Any(c => string.Equals(c.Name, name, StringComparison.Ordinal) && c.IsFocused);

    private (Window Window, SettingsViewModel Settings) ShowSettings(LinearConnectionViewModel card)
    {
        var settings = new SettingsViewModel(new Preferences(new InMemoryPreferencesStore()), Motion, _ => { }) { LinearConnection = card };
        var window = new Window { Width = 900, Height = 900, Content = new SettingsView { DataContext = settings } };
        window.Show();
        Settle(window);
        return (window, settings);
    }

    private LinearConnectionViewModel Card() => new(() => _api, Server);

    [Fact]
    public Task The_group_and_the_card_are_headings_and_the_two_key_boxes_are_masked_and_named() => OnUi(async () =>
    {
        Server.LinearConnectionAnswer = RouteAnswer.Api("linear-connection", "read-only");
        var card = Card();
        await card.Refresh(_api, Token);
        var (window, _) = ShowSettings(card);
        card.ReplaceRead();
        card.ReplaceWrite();
        Settle(window);

        Assert.Equal(("Connections", 2), (Find<TextBlock>(window, "ConnectionsTitle").Text, AutomationProperties.GetHeadingLevel((Control)Find<TextBlock>(window, "ConnectionsTitle").Parent!)));
        var read = Find<TextBox>(window, "LinearReadKey");
        var write = Find<TextBox>(window, "LinearWriteKey");
        Assert.Equal(("Linear read key", "Linear write key"), (AutomationProperties.GetName(read), AutomationProperties.GetName(write)));
        Assert.All((TextBox[])[read, write], box => Assert.NotEqual('\0', box.PasswordChar));
        window.Close();
    });

    [Fact]
    public Task The_status_region_is_in_the_tree_and_polite_before_any_check_starts() => OnUi(() =>
    {
        var (window, _) = ShowSettings(Card());

        var status = Find<TextBlock>(window, "LinearStatus");

        Assert.Equal((AutomationLiveSetting.Polite, string.Empty), (AutomationProperties.GetLiveSetting(status), status.Text ?? string.Empty));
        window.Close();
        return Task.CompletedTask;
    });

    [Fact]
    public Task The_team_boxes_are_a_labelled_group_and_Check_and_save_follows_the_ticks() => OnUi(async () =>
    {
        Server.LinearConnectionAnswer = RouteAnswer.Api("linear-connection", "not-connected");
        var card = Card();
        await card.Refresh(_api, Token);
        card.ReadKey = "lin_api_secret";
        await card.ListTeams(Token);
        var (window, _) = ShowSettings(card);

        var save = Find<Button>(window, "LinearReadSave");
        Assert.Equal(("Teams Sindri may write to", false), (AutomationProperties.GetName(Find<Control>(window, "LinearTeams")), save.IsEnabled));
        card.Teams[0].IsTicked = true;
        Settle(window);

        Assert.True(save.IsEnabled);
        window.Close();
    });

    [Fact]
    public Task An_agent_session_gets_no_Linear_row() => OnUi(async () =>
    {
        Server.LinearConnectionAnswer = RouteAnswer.Problem("linear-connection", "session-refused");
        var card = Card();
        var (window, _) = ShowSettings(card);
        Assert.True(Find<Control>(window, "LinearSettingsSlot").IsEffectivelyVisible);

        await card.Refresh(_api, Token);
        Settle(window);

        Assert.False(Find<Control>(window, "LinearSettingsSlot").IsEffectivelyVisible);
        window.Close();
    });

    [Fact]
    public Task Connect_from_the_issue_sheet_opens_Settings_with_the_read_key_box_focused_when_not_connected() => OnUi(async () =>
    {
        Server.LinearConnectionAnswer = RouteAnswer.Api("linear-connection", "not-connected");
        var (window, shell) = await Connected();
        window.SettingsSlot.Content = new SettingsView { DataContext = new SettingsViewModel(new Preferences(new InMemoryPreferencesStore()), Motion, _ => { }, shell.Setup) { LinearConnection = shell.LinearConnection } };

        shell.IssueSheet.Connect();
        await Until(window, () => Focused(window, "LinearReadKey"), "the read key box never took focus");

        Assert.True(shell.IsSettings);
        Assert.True(Find<Control>(window, "LinearConnectionCard").IsEffectivelyVisible);
    });

    [Fact]
    public Task Connect_with_read_access_already_set_focuses_the_write_key_box() => OnUi(async () =>
    {
        Server.LinearConnectionAnswer = RouteAnswer.Api("linear-connection", "read-only");
        var (window, shell) = await Connected();
        window.SettingsSlot.Content = new SettingsView { DataContext = new SettingsViewModel(new Preferences(new InMemoryPreferencesStore()), Motion, _ => { }, shell.Setup) { LinearConnection = shell.LinearConnection } };

        shell.IssueSheet.Connect();
        await Until(window, () => Focused(window, "LinearWriteKey"), "the write key box never took focus");

        Assert.True(shell.IsSettings);
    });
}
