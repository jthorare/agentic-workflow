using System.Globalization;
using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Linear;

namespace Aw.Sindri.Ui.Tests.Settings;

/// <summary>Settings → Connections → Linear (Linear hub spec §3, §5.1, §8): the read key and the teams, the write key, the words for each state, and what an agent session sees.</summary>
[Lane(TestLane.Unit)]
public sealed class LinearConnectionTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private readonly FakeSindriApi _server = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string CheckedAt => DateTimeOffset.Parse((string)GoldenFiles.Node("linear-connection", "connected")["read"]!["checkedAt"]!, CultureInfo.InvariantCulture).ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

    private LinearConnectionViewModel Card() => new(() => _api, _server);

    private async Task<LinearConnectionViewModel> Shown(string scenario)
    {
        _server.LinearConnectionAnswer = RouteAnswer.Api("linear-connection", scenario);
        var card = Card();
        await card.Refresh(_api, Token);
        return card;
    }

    // A card with a read key typed and its teams listed (none ticked).
    private async Task<LinearConnectionViewModel> Listed()
    {
        var card = await Shown("not-connected");
        card.ReadKey = "lin_api_secret";
        await card.ListTeams(Token);
        return card;
    }

    [Fact]
    public async Task Not_connected_shows_the_read_box_and_nothing_else()
    {
        var card = await Shown("not-connected");

        Assert.Equal(("Not connected", true, false, false, false), (card.Summary, card.ShowReadPaste, card.ShowReadSet, card.ShowTeams, card.ShowWrite));
    }

    [Fact]
    public async Task List_my_teams_lists_the_teams_the_key_can_see_with_none_ticked()
    {
        var card = await Listed();

        Assert.Equal(["lin_api_secret"], _server.LinearTeamKeys, StringComparer.Ordinal);
        Assert.Equal(["ENG", "OPS"], card.Teams.Select(t => t.Key), StringComparer.Ordinal);
        Assert.Equal((false, false, true), (card.Teams[0].IsTicked, card.Teams[1].IsTicked, card.ShowTeams));
        Assert.Equal(["Engineering ENG", "Operations OPS"], card.Teams.Select(t => t.AutomationName), StringComparer.Ordinal);
    }

    [Fact]
    public async Task List_my_teams_is_off_while_the_box_is_empty_and_while_the_check_runs()
    {
        var card = await Shown("not-connected");
        Assert.False(card.CanListTeams);

        card.ReadKey = "lin_api_secret";
        Assert.True(card.CanListTeams);
        _server.LinearConnectionTeamsGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = card.ListTeams(Token);

        Assert.Equal((false, Labels.LinearConnLookingUp), (card.CanListTeams, card.StatusText));
        _server.LinearConnectionTeamsGate.SetResult();
        await running;
        Assert.Equal(string.Empty, card.StatusText);
    }

    [Fact]
    public async Task With_no_team_ticked_Check_and_save_is_off_and_the_hint_says_why()
    {
        var card = await Listed();

        Assert.Equal((false, Labels.LinearConnTickOne), (card.CanSaveRead, card.SaveHint));
        card.Teams[0].IsTicked = true;
        Assert.Equal((true, Labels.LinearConnSaveHint), (card.CanSaveRead, card.SaveHint));
    }

    [Fact]
    public async Task Check_and_save_sends_the_ticked_teams_clears_the_key_and_reads_the_connection_again()
    {
        var card = await Listed();
        card.Teams[1].IsTicked = true;
        _server.LinearConnectionAnswer = RouteAnswer.Api("linear-connection", "read-only");

        await card.SaveRead(Token);

        var sent = Assert.Single(_server.LinearConnects);
        Assert.Equal(("read", "lin_api_secret"), (sent.Kind, sent.Key));
        Assert.Equal(["OPS"], sent.Teams, StringComparer.Ordinal);
        Assert.Equal((string.Empty, "Reading ENG, OPS · read-only", false, true, false), (card.ReadKey, card.Summary, card.HasError, card.ShowReadSet, card.HasWaiting));
        Assert.Equal($"Read access: connected · checked {CheckedAt}", card.ReadAccessText);
    }

    [Fact]
    public async Task A_save_that_waits_behind_other_profile_changes_says_so_and_offers_Review_changes()
    {
        var card = await Listed();
        card.Teams[0].IsTicked = true;
        _server.LinearConnectAnswer = RouteAnswer.Api("linear-connect", "read-saved-waiting");
        var reviews = 0;
        card.ReviewRequested += (_, _) => reviews++;

        await card.SaveRead(Token);
        card.RequestReview();

        Assert.Equal((true, Labels.LinearConnWaitingRead, 1), (card.HasWaiting, card.AttentionText, reviews));
    }

    [Fact]
    public async Task A_refreshed_read_forgets_a_waiting_line_the_owner_has_since_cleared()
    {
        var card = await Listed();
        card.Teams[0].IsTicked = true;
        _server.LinearConnectAnswer = RouteAnswer.Api("linear-connect", "read-saved-waiting");
        await card.SaveRead(Token);

        await card.Refresh(_api, Token);

        Assert.Equal((false, string.Empty), (card.HasWaiting, card.AttentionText));
    }

    [Fact]
    public async Task A_refused_read_key_says_so_and_a_slow_Linear_says_it_changed_nothing()
    {
        var card = await Shown("not-connected");
        card.ReadKey = "lin_api_secret";
        _server.LinearConnectionTeamsAnswer = RouteAnswer.Problem("linear-connection-teams", "refused");
        await card.ListTeams(Token);
        var refused = card.ErrorText;
        _server.LinearConnectionTeamsAnswer = RouteAnswer.Problem("linear-connection-teams", "unreachable");
        await card.ListTeams(Token);

        Assert.Equal((Labels.LinearConnReadRefused, Labels.LinearConnUnreachable, false), (refused, card.ErrorText, card.ShowTeams));
    }

    [Fact]
    public async Task The_write_box_shows_only_once_read_access_is_set()
    {
        var notConnected = await Shown("not-connected");
        var readOnly = await Shown("read-only");

        Assert.Equal((false, true, true, false), (notConnected.ShowWrite, readOnly.ShowWrite, readOnly.ShowWritePaste, readOnly.ShowWriteSet));
    }

    [Fact]
    public async Task Check_and_save_on_the_write_key_sends_it_with_no_teams_and_shows_the_connected_line()
    {
        var card = await Shown("read-only");
        card.WriteKey = "lin_api_write";
        Assert.True(card.CanCheckWrite);
        _server.LinearConnectAnswer = RouteAnswer.Api("linear-connect", "write-saved");
        _server.LinearConnectionAnswer = RouteAnswer.Api("linear-connection", "connected");

        await card.CheckWrite(Token);

        Assert.Equal([new("write", "lin_api_write", Teams: null)], _server.LinearConnects);
        Assert.Equal((string.Empty, true, $"Write access: connected · checked {CheckedAt}", "Reading ENG, OPS · writing ENG, OPS"), (card.WriteKey, card.ShowWriteSet, card.WriteAccessText, card.Summary));
    }

    [Fact]
    public async Task While_the_write_key_is_checked_the_status_names_the_teams_and_Check_and_save_is_off()
    {
        var card = await Shown("read-only");
        card.WriteKey = "lin_api_write";
        _server.LinearConnectGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var running = card.CheckWrite(Token);

        Assert.Equal((Labels.LinearConnCheckingWrite(["ENG", "OPS"]), false), (card.StatusText, card.CanCheckWrite));
        _server.LinearConnectGate.SetResult();
        await running;
    }

    [Fact]
    public async Task A_write_key_Linear_refuses_shows_the_refusal_and_stores_nothing()
    {
        var card = await Shown("read-only");
        card.WriteKey = "lin_api_write";
        _server.LinearConnectAnswer = RouteAnswer.Problem(HttpStatusCode.BadGateway, "SND-LINEAR-004", "Linear refused the write key.");

        await card.CheckWrite(Token);

        Assert.Equal((Labels.LinearConnWriteRefused, false, true, string.Empty), (card.ErrorText, card.ShowWriteSet, card.ShowWritePaste, card.WriteKey));
    }

    [Fact]
    public async Task No_keychain_and_the_same_key_as_read_show_their_own_words()
    {
        var card = await Shown("read-only");
        card.WriteKey = "lin_api_write";
        _server.LinearConnectAnswer = RouteAnswer.Problem("linear-connect", "no-keychain");
        await card.CheckWrite(Token);
        var keychain = card.ErrorText;
        card.WriteKey = "lin_api_read";
        _server.LinearConnectAnswer = RouteAnswer.Problem("linear-connect", "same-as-read");

        await card.CheckWrite(Token);

        Assert.Equal((Labels.LinearConnNoKeychain, Labels.LinearConnSameAsRead), (keychain, card.ErrorText));
    }

    [Fact]
    public async Task A_write_key_whose_scope_Linear_does_not_say_is_saved_with_a_note()
    {
        var card = await Shown("read-only");
        card.WriteKey = "lin_api_write";
        _server.LinearConnectAnswer = RouteAnswer.Api("linear-connect", "write-scope-unknown");
        _server.LinearConnectionAnswer = RouteAnswer.Api("linear-connection", "scope-unknown");

        await card.CheckWrite(Token);

        Assert.Equal((Labels.LinearConnScopeUnknown, true, false), (card.AttentionText, card.ShowWriteSet, card.HasError));
    }

    [Fact]
    public async Task Remove_key_asks_first_and_only_Remove_calls_the_disconnect()
    {
        var card = await Shown("connected");
        card.AskRemove();
        Assert.Equal((true, 0), (card.IsConfirmingRemove, _server.LinearDisconnects));
        card.KeepWrite();
        Assert.Equal((false, 0), (card.IsConfirmingRemove, _server.LinearDisconnects));
        card.AskRemove();
        _server.LinearConnectionAnswer = RouteAnswer.Api("linear-connection", "read-only");

        await card.RemoveWrite(Token);

        Assert.Equal((1, false, false, true, "Reading ENG, OPS · read-only"), (_server.LinearDisconnects, card.IsConfirmingRemove, card.ShowWriteSet, card.ShowWritePaste, card.Summary));
    }

    [Fact]
    public async Task A_remove_the_keychain_refuses_shows_its_line_closes_the_question_and_keeps_write_connected()
    {
        var card = await Shown("connected");
        card.AskRemove();
        _server.LinearDisconnectWriteAnswer = RouteAnswer.Problem(HttpStatusCode.UnprocessableEntity, "SND-LINEAR-014", "No keychain to delete the write key from.");

        await card.RemoveWrite(Token);

        Assert.Equal((Labels.LinearConnRemoveKeychainFailed, false, true), (card.ErrorText, card.IsConfirmingRemove, card.ShowWriteSet));
    }

    [Fact]
    public async Task Replace_key_brings_the_paste_box_back_for_each_key()
    {
        var card = await Shown("connected");

        card.ReplaceWrite();
        card.ReplaceRead();

        Assert.Equal((true, false, true, false), (card.ShowWritePaste, card.ShowWriteSet, card.ShowReadPaste, card.ShowReadSet));
    }

    [Fact]
    public async Task An_agent_session_is_refused_the_connection_and_gets_no_card()
    {
        _server.LinearConnectionAnswer = RouteAnswer.Problem("linear-connection", "session-refused");
        var card = Card();
        Assert.True(card.IsAvailable);

        await card.Refresh(_api, Token);

        Assert.Equal((false, false), (card.IsAvailable, card.HasError));
    }

    [Fact]
    public async Task Reveal_is_raised_once_the_next_read_is_in_and_not_before()
    {
        _server.LinearConnectionAnswer = RouteAnswer.Api("linear-connection", "not-connected");
        var card = Card();
        var revealed = 0;
        card.RevealRequested += (_, _) => revealed++;

        card.Reveal();
        var before = revealed;
        await card.Refresh(_api, Token);
        await card.Refresh(_api, Token);

        Assert.Equal((0, 1), (before, revealed));
    }

    [Fact]
    public void Reveal_with_no_API_known_is_raised_at_once()
    {
        var card = new LinearConnectionViewModel(() => null, _server);
        var revealed = 0;
        card.RevealRequested += (_, _) => revealed++;

        card.Reveal();

        Assert.Equal(1, revealed);
    }

    // A connection answer built by hand: the read and write states, and the profile's team keys.
    private static RouteAnswer Connection(string read, string write, bool pending, params string[] teams) =>
        new(HttpStatusCode.OK,
            $$"""{"read":{"state":"{{read}}","checkedAt":"2026-01-01T09:58:00.0000000Z"},"write":{"state":"{{write}}","checkedAt":"2026-01-01T09:58:00.0000000Z"},"teams":[{{string.Join(',', teams.Select(k => $$"""{"id":"00000000-0000-4000-8000-0000000000e1","key":"{{k}}","name":"{{k}}"}"""))}}],"pending":{{(pending ? "true" : "false")}}}""");

    [Fact]
    public async Task A_first_read_that_is_pending_with_no_save_here_says_the_change_waits_not_that_it_was_saved()
    {
        var card = await Shown("pending");

        Assert.Equal((true, Labels.LinearConnPending), (card.HasWaiting, card.AttentionText));
    }

    [Fact]
    public async Task A_save_that_waits_keeps_its_own_line_through_the_refresh_its_own_profile_event_causes()
    {
        var card = await Listed();
        card.Teams[0].IsTicked = true;
        _server.LinearConnectAnswer = RouteAnswer.Api("linear-connect", "read-saved-waiting");
        _server.LinearConnectionAnswer = RouteAnswer.Api("linear-connection", "pending");
        await card.SaveRead(Token);

        await card.Refresh(_api, Token);

        Assert.Equal((true, Labels.LinearConnWaitingRead), (card.HasWaiting, card.AttentionText));
    }

    [Fact]
    public async Task Listing_after_Replace_key_ticks_the_teams_already_stored()
    {
        _server.LinearConnectionAnswer = Connection("resolves", "notSet", pending: false, "OPS");
        var card = Card();
        await card.Refresh(_api, Token);
        card.ReplaceRead();
        card.ReadKey = "lin_api_new";

        await card.ListTeams(Token);

        Assert.Equal([false, true], [.. card.Teams.Select(t => t.IsTicked)]);
    }

    [Fact]
    public async Task Cancel_on_the_read_line_goes_back_to_the_set_line_and_calls_nothing()
    {
        var card = await Shown("read-only");
        card.ReplaceRead();
        card.ReadKey = "lin_api_half_typed";

        card.CancelReplaceRead();

        Assert.Equal((true, false, string.Empty, 0, 0), (card.ShowReadSet, card.ShowReadPaste, card.ReadKey, _server.LinearTeamKeys.Count, _server.LinearConnects.Count));
    }

    [Fact]
    public async Task Cancel_on_the_read_line_is_off_while_the_replacement_key_is_saved()
    {
        var card = await Shown("read-only");
        card.ReplaceRead();
        card.ReadKey = "lin_api_new";
        await card.ListTeams(Token);
        card.Teams[0].IsTicked = true;
        _server.LinearConnectGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var running = card.SaveRead(Token);

        Assert.Equal((true, false), (card.ShowCancelRead, card.CanCancelRead));   // still drawn, just not pressable
        _server.LinearConnectGate.SetResult();
        await running;
    }

    [Fact]
    public async Task Cancel_on_the_write_line_is_off_while_the_replacement_key_is_checked()
    {
        var card = await Shown("connected");
        card.ReplaceWrite();
        card.WriteKey = "lin_api_new_write";
        _server.LinearConnectGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var running = card.CheckWrite(Token);

        Assert.Equal((true, false), (card.ShowCancelWrite, card.CanCancelWrite));
        _server.LinearConnectGate.SetResult();
        await running;
    }

    [Fact]
    public async Task Cancel_on_the_write_line_goes_back_to_the_set_line_and_calls_nothing()
    {
        var card = await Shown("connected");
        card.ReplaceWrite();
        card.WriteKey = "lin_api_half_typed";

        card.CancelReplaceWrite();

        Assert.Equal((true, false, string.Empty, 0), (card.ShowWriteSet, card.ShowWritePaste, card.WriteKey, _server.LinearConnects.Count));
    }

    [Fact]
    public async Task A_refused_read_key_with_a_write_key_stored_still_draws_the_write_line_with_Remove()
    {
        _server.LinearConnectionAnswer = Connection("refused", "resolves", pending: false, "ENG");
        var card = Card();

        await card.Refresh(_api, Token);

        Assert.Equal((false, true, true, false), (card.ReadIsSet, card.ShowWrite, card.ShowWriteSet, card.ShowWritePaste));
    }

    [Fact]
    public async Task A_stored_write_key_of_unknown_scope_loses_the_Saved_prefix_on_a_later_read()
    {
        var card = await Shown("scope-unknown");

        Assert.Equal(Labels.LinearConnScopeUnknownRead, card.AttentionText);
    }

    [Fact]
    public void Reveal_with_no_API_known_waits_for_the_view_to_attach_and_is_raised_then()
    {
        var card = new LinearConnectionViewModel(() => null, _server);
        card.Reveal();
        var revealed = 0;
        card.RevealRequested += (_, _) => revealed++;

        card.RevealIfWaiting();

        Assert.Equal(1, revealed);
    }
}
