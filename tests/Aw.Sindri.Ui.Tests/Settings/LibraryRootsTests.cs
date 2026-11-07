using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Reviews;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Settings;

/// <summary>Settings' Library card (review page spec §6.3, §16): the folders per kind, the loading and empty words, the Add a folder form with its field errors, Remove on vaults, Turn off on aw.</summary>
[Lane(TestLane.Unit)]
public sealed class LibraryRootsTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private readonly FakeSindriApi _server = new();
    private readonly FakeFolderPicker _picker = new();
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private LibraryRootsViewModel Card() => new(() => _api, _server, _picker, _clock);

    private async Task<LibraryRootsViewModel> Shown(string scenario = "ok")
    {
        _server.LibraryAnswer = RouteAnswer.Api("library", scenario);
        var card = Card();
        await card.Refresh(_api, Token);
        return card;
    }

    private static LibraryFolderViewModel Row(LibraryRootsViewModel card, string name) => card.Rows.Single(r => string.Equals(r.Name, name, StringComparison.Ordinal));

    private async Task<LibraryRootsViewModel> Filled(string name = "harbor-notes", string path = "~/Documents/Harbor", string deny = "Private/**, Journal/**")
    {
        var card = await Shown();
        card.Form.Open();
        (card.Form.Name, card.Form.Path, card.Form.Deny) = (name, path, deny);
        return card;
    }

    [Fact]
    public void Before_the_first_answer_the_card_says_it_is_reading_and_offers_no_add()
    {
        var card = Card();

        Assert.Equal((true, false, false, false, Labels.LibraryFoldersReading), (card.IsLoading, card.IsEmpty, card.HasRows, card.CanAdd, card.ReadingText));
    }

    [Fact]
    public async Task The_folders_come_in_served_order_with_a_path_line_and_the_scope_for_a_repo()
    {
        var card = await Shown();

        Assert.Equal(["aw", "work", "inbox", "repo:agentic-workflow", "repo:harbor"], card.Rows.Select(r => r.Name), StringComparer.Ordinal);
        Assert.Equal(
            ["~/.agentic-workflow", "~/Documents/WorkVault", "~/Documents/Inbox", "jthorare/agentic-workflow · docs/**", "acme/harbor · docs/**"],
            card.Rows.Select(r => r.PathText),
            StringComparer.Ordinal);
        Assert.Equal(
            [Labels.LibraryFoldersBuiltIn, Labels.LibraryFoldersVault, Labels.LibraryFoldersVault, Labels.LibraryFoldersFromRepos, Labels.LibraryFoldersFromRepos],
            card.Rows.Select(r => r.KindText),
            StringComparer.Ordinal);
        Assert.Equal((false, false, true, true), (card.IsLoading, card.IsEmpty, card.HasRows, card.CanAdd));
    }

    [Fact]
    public async Task A_read_folder_says_when_and_how_many_docs_and_errors_and_an_unread_one_says_reading()
    {
        var card = await Shown("with-errors");

        Assert.Equal("scanned 3 min ago, 9 docs, 0 errors", Row(card, "aw").StatusText);
        Assert.Equal("scanned 3 min ago, 20 docs, 2 errors", Row(card, "work").StatusText);
        Assert.Equal("scanned 3 min ago, 8 docs, 1 error", Row(card, "repo:agentic-workflow").StatusText);
        Assert.Equal((Labels.LibraryFoldersReading, true), (Row(card, "inbox").StatusText, Row(card, "inbox").IsReading));
        Assert.False(Row(card, "work").IsReading);
    }

    [Fact]
    public async Task An_unreadable_folder_says_why()
    {
        var card = await Shown("root-unreadable");

        var notes = Row(card, "notes");
        Assert.Equal(("can't be read: permission denied", true, false), (notes.StatusText, notes.IsUnreadable, notes.IsReading));
    }

    [Fact]
    public async Task Each_kind_gets_its_own_control_Remove_on_vaults_Turn_off_on_aw_and_repo_roots_are_read_only()
    {
        var card = await Shown();

        Assert.Equal((false, true, false, false), (Row(card, "aw").CanRemove, Row(card, "aw").CanTurnOff, Row(card, "aw").CanTurnOn, Row(card, "aw").IsReadOnly));
        Assert.Equal((true, false, false, false), (Row(card, "work").CanRemove, Row(card, "work").CanTurnOff, Row(card, "work").CanTurnOn, Row(card, "work").IsReadOnly));
        var repo = Row(card, "repo:harbor");
        Assert.Equal((false, false, false, true, Labels.LibraryFoldersReadOnly), (repo.CanRemove, repo.CanTurnOff, repo.CanTurnOn, repo.IsReadOnly, repo.ReadOnlyText));
    }

    [Fact]
    public async Task A_turned_off_aw_folder_says_so_and_offers_Turn_on()
    {
        var card = await Shown("empty");

        var aw = Row(card, "aw");
        Assert.Equal((Labels.LibraryFoldersOff, true, false, true), (aw.StatusText, aw.IsOff, aw.CanTurnOff, aw.CanTurnOn));
    }

    [Fact]
    public async Task A_session_gets_no_folders_so_the_card_shows_the_empty_words_and_nothing_to_edit()
    {
        var card = await Shown("session");

        Assert.Equal((true, false, false, Labels.LibraryFoldersNone), (card.IsEmpty, card.HasRows, card.CanAdd, card.EmptyText));
        Assert.False(card.Form.IsOpen);
    }

    [Fact]
    public async Task A_failed_first_read_says_why_and_a_failed_later_read_keeps_the_folders()
    {
        _server.LibraryAnswer = RouteAnswer.Problem(System.Net.HttpStatusCode.ServiceUnavailable, "SND-API-503", "Sindri is busy (fix: wait)");
        var card = Card();
        await card.Refresh(_api, Token);
        Assert.Contains("Sindri is busy", card.Error, StringComparison.Ordinal);

        _server.LibraryAnswer = RouteAnswer.Api("library", "ok");
        await card.Refresh(_api, Token);
        Assert.Equal((5, string.Empty), (card.Rows.Count, card.Error));

        _server.LibraryAnswer = RouteAnswer.Problem(System.Net.HttpStatusCode.ServiceUnavailable, "SND-API-503", "Sindri is busy (fix: wait)");
        await card.Refresh(_api, Token);
        Assert.Equal((5, string.Empty), (card.Rows.Count, card.Error));
    }

    // The Add a folder form

    [Fact]
    public async Task Add_folder_opens_the_form_and_Cancel_closes_it_and_clears_what_was_typed()
    {
        var card = await Shown();
        Assert.True(card.CanAdd);

        card.Form.Open();
        Assert.Equal((true, false), (card.Form.IsOpen, card.Form.CanSave));
        (card.Form.Name, card.Form.Path) = ("harbor-notes", "~/Documents/Harbor");
        Assert.True(card.Form.CanSave);

        card.Form.Cancel();

        Assert.Equal((false, string.Empty, string.Empty), (card.Form.IsOpen, card.Form.Name, card.Form.Path));
        Assert.Empty(_server.LibraryRootAdds);
    }

    [Fact]
    public async Task Save_needs_a_name_and_a_folder()
    {
        var card = await Shown();
        card.Form.Open();

        card.Form.Name = "harbor-notes";
        Assert.False(card.Form.CanSave);

        card.Form.Path = "  ";
        Assert.False(card.Form.CanSave);

        card.Form.Path = "~/Documents/Harbor";
        Assert.True(card.Form.CanSave);
    }

    [Fact]
    public async Task Browse_puts_the_chosen_folder_in_the_field_and_no_choice_keeps_what_was_typed()
    {
        var card = await Shown();
        card.Form.Open();
        card.Form.Path = "~/typed";
        _picker.Queue(folder: null);
        await card.Form.Browse(Token);
        Assert.Equal("~/typed", card.Form.Path);

        _picker.Queue("/home/joi/Documents/Harbor");
        await card.Form.Browse(Token);

        Assert.Equal(("/home/joi/Documents/Harbor", true), (card.Form.Path, card.Form.CanBrowse));
        Assert.Equal([Labels.LibraryFoldersBrowseTitle, Labels.LibraryFoldersBrowseTitle], _picker.Asked, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Save_and_approve_sends_the_name_folder_and_deny_globs_with_approve_true_then_closes_and_reads_again()
    {
        var card = await Filled();
        var reads = _server.LibraryCalls;

        await card.Form.Save(Token);

        var sent = Assert.Single(_server.LibraryRootAdds);
        Assert.Equal(("harbor-notes", "~/Documents/Harbor", true), (sent.Name, sent.Path, sent.Approve));
        Assert.Equal(["Private/**", "Journal/**"], sent.Deny, StringComparer.Ordinal);
        Assert.Equal((false, string.Empty, reads + 1), (card.Form.IsOpen, card.Note, _server.LibraryCalls));
        Assert.Equal((string.Empty, string.Empty), (card.Form.Name, card.Form.Path));
    }

    [Fact]
    public async Task An_empty_deny_field_sends_no_globs()
    {
        var card = await Filled(deny: "  ");

        await card.Form.Save(Token);

        Assert.Empty(Assert.Single(_server.LibraryRootAdds).Deny);
    }

    [Fact]
    public async Task A_save_that_waits_for_the_profile_approval_says_so_and_closes_the_form()
    {
        var card = await Filled();
        _server.LibraryRootAnswer = RouteAnswer.Api("library-roots", "added-pending-others");

        await card.Form.Save(Token);

        Assert.Equal((Labels.ReviewsLoginPending, true, false), (card.Note, card.HasNote, card.Form.IsOpen));
    }

    [Fact]
    public async Task A_later_approved_change_clears_the_waiting_line()
    {
        var card = await Filled();
        _server.LibraryRootAnswer = RouteAnswer.Api("library-roots", "added-pending-others");
        await card.Form.Save(Token);
        _server.LibraryRootAnswer = RouteAnswer.Api("library-roots", "removed");

        await card.Remove(Row(card, "work"), Token);

        Assert.Equal((string.Empty, false), (card.Note, card.HasNote));
    }

    [Theory]
    [InlineData("bad-name", "name")]
    [InlineData("duplicate-name", "name")]
    [InlineData("path-not-absolute", "path")]
    [InlineData("path-too-wide", "path")]
    [InlineData("too-many-vaults", "path")]
    [InlineData("inside-another", "path")]
    [InlineData("bad-deny", "deny")]
    public async Task A_400_puts_its_detail_under_the_field_it_names_and_keeps_the_form_open(string scenario, string field)
    {
        var card = await Filled();
        _server.LibraryRootAnswer = RouteAnswer.Problem("library-roots", scenario);
        var detail = System.Text.Json.JsonDocument.Parse(GoldenFiles.Api("library-roots", scenario)).RootElement.GetProperty("detail").GetString();

        await card.Form.Save(Token);

        var form = card.Form;
        var shown = new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = form.NameError, ["path"] = form.PathError, ["deny"] = form.DenyError };
        Assert.Equal(detail, shown[field]);
        Assert.All(shown.Where(s => !string.Equals(s.Key, field, StringComparison.Ordinal)), s => Assert.Equal(string.Empty, s.Value));
        Assert.Equal((true, string.Empty, "harbor-notes", "~/Documents/Harbor"), (form.IsOpen, form.FormError, form.Name, form.Path));
        Assert.False(card.HasNote);
    }

    [Fact]
    public async Task Editing_a_field_clears_its_error()
    {
        var card = await Filled();
        _server.LibraryRootAnswer = RouteAnswer.Problem("library-roots", "bad-name");
        await card.Form.Save(Token);
        Assert.True(card.Form.HasNameError);

        card.Form.Path = "~/Documents/Elsewhere";
        Assert.True(card.Form.HasNameError);

        card.Form.Name = "harbor";
        Assert.Equal((string.Empty, false), (card.Form.NameError, card.Form.HasNameError));
    }

    [Fact]
    public async Task A_403_shows_the_refusal_with_its_code_and_keeps_the_form_open()
    {
        var card = await Filled();
        _server.LibraryRootAnswer = RouteAnswer.Problem("library-roots", "session-refused");

        await card.Form.Save(Token);

        Assert.Contains("a session can't change them", card.Form.FormError, StringComparison.Ordinal);
        Assert.Contains("SND-AUTH-002", card.Form.FormError, StringComparison.Ordinal);
        Assert.Equal((true, true, string.Empty), (card.Form.IsOpen, card.Form.HasFormError, card.Form.NameError));
    }

    [Fact]
    public async Task While_a_save_is_on_its_way_it_says_so_and_a_second_press_sends_nothing()
    {
        var card = await Filled();
        _server.LibraryRootGate = new TaskCompletionSource();

        var first = card.Form.Save(Token);
        Assert.Equal((true, false, Labels.LibraryFoldersSaving), (card.Form.IsSending, card.Form.CanSave, card.Form.SaveText));
        await card.Form.Save(Token);
        _server.LibraryRootGate.SetResult();
        await first;

        _ = Assert.Single(_server.LibraryRootAdds);
        Assert.Equal((false, Labels.LibraryFoldersSave), (card.Form.IsSending, card.Form.SaveText));
    }

    // Remove and Turn off

    [Fact]
    public async Task Remove_asks_first_and_Cancel_sends_nothing()
    {
        var card = await Shown();
        var work = Row(card, "work");

        work.AskRemove();
        Assert.Equal((true, Labels.LibraryFoldersRemoveAsk("work")), (work.IsConfirmingRemove, work.ConfirmText));

        work.CancelRemove();

        Assert.False(work.IsConfirmingRemove);
        Assert.Empty(_server.LibraryRootRemoves);
    }

    [Fact]
    public async Task Confirmed_Remove_sends_the_vaults_name_with_approve_true_and_reads_again()
    {
        var card = await Shown();
        var reads = _server.LibraryCalls;
        _server.LibraryRootAnswer = RouteAnswer.Api("library-roots", "removed");

        await card.Remove(Row(card, "work"), Token);

        Assert.Equal([("work", true)], _server.LibraryRootRemoves);
        Assert.Equal((reads + 1, string.Empty), (_server.LibraryCalls, card.Error));
    }

    [Fact]
    public async Task A_refused_Remove_shows_the_refusal_under_the_list()
    {
        var card = await Shown();
        _server.LibraryRootAnswer = RouteAnswer.Problem("library-roots", "remove-not-vault");

        await card.Remove(Row(card, "aw"), Token);

        Assert.Contains("isn't a vault", card.Error, StringComparison.Ordinal);
        Assert.Contains("SND-LIB-001", card.Error, StringComparison.Ordinal);
        Assert.True(card.HasError);
    }

    [Fact]
    public async Task Turn_off_and_Turn_on_send_the_aw_switch_with_approve_true()
    {
        var card = await Shown();
        _server.LibraryRootAnswer = RouteAnswer.Api("library-roots", "aw-off");

        await card.TurnAw(on: false, Token);
        await card.TurnAw(on: true, Token);

        Assert.Equal([(false, true), (true, true)], _server.LibraryAwSwitches.Select(s => (s.On, s.Approve)));
    }

    [Fact]
    public async Task A_turn_off_that_waits_for_the_approval_says_so()
    {
        var card = await Shown();
        _server.LibraryRootAnswer = RouteAnswer.Api("library-roots", "added-pending-others");

        await card.TurnAw(on: false, Token);

        Assert.Equal(Labels.ReviewsLoginPending, card.Note);
    }

    [Fact]
    public async Task The_rows_and_buttons_carry_screen_reader_names()
    {
        var card = await Shown();

        Assert.Equal("Turn off aw", Row(card, "aw").AwName);
        Assert.Equal("Remove work", Row(card, "work").RemoveName);
        Assert.Equal("work, vault, ~/Documents/WorkVault, scanned 3 min ago, 20 docs, 0 errors", Row(card, "work").AutomationName);
        Assert.Equal("repo:harbor, from your repos, acme/harbor · docs/**, scanned 3 min ago, 4 docs, 0 errors, set by the repo list", Row(card, "repo:harbor").AutomationName);
    }

    [Fact]
    public async Task A_folder_with_errors_gets_a_chip_with_a_name_and_one_without_gets_none()
    {
        var card = await Shown("with-errors");

        var work = Row(card, "work");
        Assert.Equal((true, "2 errors", "2 errors in work"), (work.HasErrors, work.ErrorsText, work.ErrorsName));
        var repo = Row(card, "repo:agentic-workflow");
        Assert.Equal((true, "1 error", "1 error in repo:agentic-workflow"), (repo.HasErrors, repo.ErrorsText, repo.ErrorsName));
        Assert.False(Row(card, "aw").HasErrors);
        Assert.False(Row(card, "inbox").HasErrors);
    }

    [Fact]
    public async Task Only_vault_rows_say_who_can_read_their_docs()
    {
        var card = await Shown();

        Assert.Equal(
            [("aw", false), ("work", true), ("inbox", true), ("repo:agentic-workflow", false), ("repo:harbor", false)],
            card.Rows.Select(r => (r.Name, r.HasVaultNote)));
        Assert.Equal(Labels.LibraryFoldersVaultNote, Row(card, "work").VaultNoteText);
    }

    [Fact]
    public async Task An_event_refresh_keeps_the_row_objects_so_an_open_Remove_question_stays_up()
    {
        var card = await Shown();
        var work = Row(card, "work");
        work.AskRemove();

        await card.Refresh(_api, Token);

        Assert.Same(work, Row(card, "work"));
        Assert.True(Row(card, "work").IsConfirmingRemove);
    }

    [Fact]
    public async Task A_refresh_updates_a_kept_row_and_drops_a_vault_that_is_gone()
    {
        var card = await Shown("ok");
        var work = Row(card, "work");

        _server.LibraryAnswer = RouteAnswer.Api("library", "with-errors");
        await card.Refresh(_api, Token);
        Assert.Equal((work, "2 errors", true), (Row(card, "work"), Row(card, "work").ErrorsText, Row(card, "work").HasErrors));

        _server.LibraryAnswer = RouteAnswer.Api("library", "near-misses-only");
        await card.Refresh(_api, Token);
        Assert.DoesNotContain(card.Rows, r => string.Equals(r.Name, "inbox", StringComparison.Ordinal));
    }

    [Fact]
    public async Task While_a_change_is_in_flight_every_button_is_disabled_and_a_confirmed_Remove_stays_asked_instead_of_vanishing()
    {
        var card = await Shown();
        var work = Row(card, "work");
        work.AskRemove();
        _server.LibraryRootGate = new TaskCompletionSource();

        var turning = card.TurnAw(on: false, Token);
        Assert.Equal((false, false, false), (Row(card, "aw").IsIdle, work.IsIdle, work.CanAsk));
        await card.Remove(work, Token);

        Assert.Equal((true, 0), (work.IsConfirmingRemove, _server.LibraryRootRemoves.Count));
        _server.LibraryRootGate.SetResult();
        await turning;
        Assert.Equal((true, true), (Row(card, "aw").IsIdle, Row(card, "work").IsIdle));

        await card.Remove(Row(card, "work"), Token);

        Assert.Equal([("work", true)], _server.LibraryRootRemoves);
    }

    [Fact]
    public async Task An_older_read_that_lands_last_does_not_bring_back_a_dropped_folder()
    {
        var card = Card();
        var (first, second) = (new TaskCompletionSource(), new TaskCompletionSource());
        _server.GateNextLibraryRead(first);
        _server.GateNextLibraryRead(second);
        _server.LibraryAnswer = RouteAnswer.Api("library", "ok");
        var older = card.Refresh(_api, Token);
        _server.LibraryAnswer = RouteAnswer.Api("library", "empty");
        var newer = card.Refresh(_api, Token);

        second.SetResult();
        await newer;
        Assert.Equal(["aw", "repo:agentic-workflow", "repo:harbor"], card.Rows.Select(r => r.Name), StringComparer.Ordinal);
        first.SetResult();
        await older;

        Assert.Equal(["aw", "repo:agentic-workflow", "repo:harbor"], card.Rows.Select(r => r.Name), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Cancel_is_disabled_while_the_save_is_on_its_way_so_its_answer_cannot_land_on_a_closed_form()
    {
        var card = await Filled();
        _server.LibraryRootGate = new TaskCompletionSource();

        var saving = card.Form.Save(Token);
        Assert.False(card.Form.CanCancel);
        card.Form.Cancel();
        card.CloseForm();
        Assert.Equal((true, "harbor-notes"), (card.Form.IsOpen, card.Form.Name));

        _server.LibraryRootGate.SetResult();
        await saving;

        Assert.Equal((false, true), (card.Form.IsOpen, card.Form.CanCancel));
    }

    [Fact]
    public async Task A_failed_change_reads_again_so_a_vault_removed_elsewhere_drops_its_row()
    {
        var card = await Shown("ok");
        var reads = _server.LibraryCalls;
        _server.LibraryRootAnswer = RouteAnswer.Problem("library-roots", "remove-unknown");
        _server.LibraryAnswer = RouteAnswer.Api("library", "near-misses-only");

        await card.Remove(Row(card, "inbox"), Token);

        Assert.Equal(reads + 1, _server.LibraryCalls);
        Assert.DoesNotContain(card.Rows, r => string.Equals(r.Name, "inbox", StringComparison.Ordinal));
        Assert.True(card.HasError);
    }

    [Fact]
    public async Task A_refresh_after_a_failed_change_keeps_the_failure_text_and_a_later_change_clears_it()
    {
        var card = await Shown();
        _server.LibraryRootAnswer = RouteAnswer.Problem("library-roots", "remove-not-vault");
        await card.Remove(Row(card, "aw"), Token);
        var failure = card.Error;
        Assert.NotEqual(0, failure.Length);

        await card.Refresh(_api, Token);

        Assert.Equal((failure, failure), (card.Error, card.ChangeError));

        _server.LibraryRootAnswer = RouteAnswer.Api("library-roots", "removed");
        await card.Remove(Row(card, "work"), Token);
        Assert.Equal((string.Empty, false), (card.Error, card.HasError));
    }

    [Fact]
    public async Task The_errors_chip_asks_for_the_library_errors_sheet()
    {
        var card = await Shown("with-errors");
        var asked = 0;
        card.ErrorsRequested += (_, _) => asked++;

        card.OpenErrors();

        Assert.Equal(1, asked);
    }

    [Fact]
    public async Task Editing_the_form_clears_its_own_refusal_line()
    {
        var card = await Filled();
        _server.LibraryRootAnswer = RouteAnswer.Problem("library-roots", "session-refused");
        await card.Form.Save(Token);
        Assert.True(card.Form.HasFormError);

        card.Form.Name = "harbor";

        Assert.Equal((string.Empty, false), (card.Form.FormError, card.Form.HasFormError));
    }

    [Fact]
    public async Task A_field_member_that_is_not_a_string_means_no_field_so_the_line_is_the_forms_own()
    {
        var card = await Filled();
        _server.LibraryRootAnswer = new RouteAnswer(System.Net.HttpStatusCode.BadRequest, """{"type":"about:blank","title":"SND-LIB-001","status":400,"detail":"nope (fix: x)","field":5,"traceId":"<trace>"}""");

        await card.Form.Save(Token);

        Assert.Equal((true, string.Empty, string.Empty, string.Empty), (card.Form.HasFormError, card.Form.NameError, card.Form.PathError, card.Form.DenyError));
    }
}
