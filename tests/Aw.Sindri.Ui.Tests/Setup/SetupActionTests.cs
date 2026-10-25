using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Setup;
using static Aw.Sindri.Ui.Tests.Setup.SetupFixture;

namespace Aw.Sindri.Ui.Tests.Setup;

/// <summary>The one action a step offers: an app button when the app can do it, otherwise the terminal command with Copy.</summary>
[Lane(TestLane.Unit)]
public sealed class SetupActionTests
{
    private readonly SetupFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SetupStepViewModel Step(SetupViewModel setup, string id) => setup.Steps.Single(s => s.Id == id);

    [Fact]
    public async Task A_step_with_no_action_has_none()
    {
        _f.Serve("first-run");

        var step = Step(await _f.Read(Token), "profile");

        Assert.Null(step.Action);
    }

    [Fact]
    public async Task Approve_all_is_an_app_button_when_the_app_can_open_it()
    {
        var f = new SetupFixture("extra-approve-all-ready", "ApproveAllRecipes");   // listed cli-only, so the row has no plan of its own; the app opens the dialog itself, one click
        f.Serve(With("first-run", "recipes", "needs-you", "ApproveAllRecipes"));
        _ = f.InApp.Add("ApproveAllRecipes");
        var setup = await f.Read(Token);
        var asked = new List<string>();
        setup.ActionRequested += (_, e) => asked.Add(e.Action);

        var action = Step(setup, "recipes").Action!;
        action.Open();

        Assert.Equal(("Approve all workflows", true, null, false), (action.Label, action.IsInApp, action.Cli, action.HasProtected));
        Assert.Equal("Approve all workflows, Approve the workflows", action.Name);
        Assert.Equal(["ApproveAllRecipes"], asked, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_protected_action_the_app_cannot_open_shows_the_terminal_copy_of_the_catalogs_command()
    {
        var f = new SetupFixture("extra-approve-all-ready", "ApproveAllRecipes");   // listed cli-only and the app does not open it here (no InApp entry), so it keeps the terminal copy
        f.Serve(With("first-run", "recipes", "needs-you", "ApproveAllRecipes"));

        var action = Step(await f.Read(Token), "recipes").Action!;

        Assert.Equal((false, null, true), (action.IsInApp, action.Cli, action.HasProtected));
        Assert.Equal("sindri recipe approve-all", action.Protected!.Cli);   // the catalog's own CLI form wins over the app's table
        Assert.Equal("⌂ Approve all workflows · terminal", action.Protected.Label);
        await action.Protected.Copy(Token);
        Assert.Equal("sindri recipe approve-all", f.Clipboard.Text);
    }

    [Fact]
    public async Task The_profile_approval_is_terminal_copy_until_the_app_can_open_it()
    {
        var f = new SetupFixture("extra-approve-all-ready", "ApproveProfile");
        f.Serve("profile-needs-approval");
        var setup = await f.Read(Token);

        var action = Step(setup, "profile").Action!;

        Assert.Equal((true, "⌂ Review and approve · terminal"), (action.HasProtected, action.Protected!.Label));
    }

    [Fact]
    public async Task An_action_the_catalog_does_not_list_shows_the_spec_command()
    {
        var f = new SetupFixture("extra-approve-all-ready");   // this catalog has no AddRepo row
        f.Serve("first-run");

        var action = Step(await f.Read(Token), "repo").Action!;

        Assert.Equal(("Add a repo", "sindri repo add <path>", false), (action.Label, action.Cli?.Cli, action.IsInApp));
    }

    [Fact]
    public async Task An_action_on_a_done_step_reads_again_or_another()
    {
        var f = new SetupFixture("extra-approve-all-ready");   // this catalog has no AddRepo row; where the served one is ready the form stands in for the button
        f.Serve(With("first-run", "repo", "done", "AddRepo"));

        var action = Step(await f.Read(Token), "repo").Action!;

        Assert.Equal("Add another repo", action.Label);
    }

    [Fact]
    public async Task An_action_this_build_does_not_know_is_not_offered()
    {
        _f.Serve(With("first-run", "repo", "needs-you", "ReticulateSplines"));

        var step = Step(await _f.Read(Token), "repo");

        Assert.Null(step.Action);
    }

    [Fact]
    public async Task The_map_row_offers_Retry_only_when_it_failed_and_nothing_when_it_works()
    {
        var failed = await ReadWith("failed");
        var working = await ReadWith("running-progress");
        var retrying = await ReadWith("map-retrying");   // the server serves RefreshContextMap on a running map: the app still shows no button

        var retry = Step(failed, "map").Action!;
        Assert.Equal(("RefreshContextMap", "Retry", true, "Retry, Build the product map"), (retry.Action, retry.Label, retry.IsInApp, retry.Name));
        Assert.Null(Step(working, "map").Action);
        Assert.Null(Step(retrying, "map").Action);
    }

    [Fact]
    public async Task Retry_starts_the_refresh_once_and_the_row_shows_working_until_the_next_read()
    {
        var setup = await ReadWith("failed");

        await setup.RefreshMap(Token);

        var map = Step(setup, "map");
        Assert.Equal((1, "Working", "Building the map again.", false), (_f.Server.RefreshContextMapCalls, map.ChipWord, map.Note, map.HasAction));
        await setup.Refresh(Url, Token);
        Assert.Equal(("Failed", true), (map.ChipWord, map.HasAction));   // the server's word wins again
    }

    [Fact]
    public async Task One_press_sends_one_refresh_even_when_a_read_was_in_flight_and_a_second_press_follows()
    {
        var setup = await ReadWith("failed");
        _f.Server.SetupGate = new TaskCompletionSource();
        var inFlight = setup.Refresh(Url, Token);   // a read started before the press: it still says failed, and lands after it

        await setup.RefreshMap(Token);
        _f.Server.SetupGate.SetResult();
        await inFlight;
        await setup.RefreshMap(Token);   // a second press on a button that must not be there any more

        var map = Step(setup, "map");
        Assert.Equal((1, "Working", false), (_f.Server.RefreshContextMapCalls, map.ChipWord, map.HasAction));
        _f.Server.SetupGate = null;
        await setup.Refresh(Url, Token);   // the read that follows the send brings the server's word back
        Assert.Equal(("Failed", true), (map.ChipWord, map.HasAction));
    }

    [Fact]
    public async Task A_409_without_a_running_job_is_an_error_not_already_building()
    {
        var setup = await ReadWith("failed");
        _f.Server.RefreshContextMapAnswer = RouteAnswer.Problem("context-map-refresh", "refused-ended");

        await setup.RefreshMap(Token);

        var map = Step(setup, "map");
        Assert.Equal(("Failed", true), (map.ChipWord, map.Note.Length > 0 && !map.Note.StartsWith("Already", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_409_with_the_running_job_shows_the_map_as_already_building_not_as_an_error()
    {
        var setup = await ReadWith("failed");
        _f.Server.RefreshContextMapAnswer = RouteAnswer.Problem("context-map-refresh", "refused-running");

        await setup.RefreshMap(Token);

        var map = Step(setup, "map");
        Assert.Equal(("Working", "Already building the map.", false), (map.ChipWord, map.Note, map.HasError));
    }

    [Fact]
    public async Task Any_other_refusal_of_the_refresh_is_said_in_words_and_the_row_stays_failed()
    {
        var setup = await ReadWith("failed");
        _f.Server.RefreshContextMapAnswer = RouteAnswer.Problem(HttpStatusCode.Forbidden, "SND-AUTH-002", "An agent session can't do this.");

        await setup.RefreshMap(Token);

        var map = Step(setup, "map");
        Assert.Equal(("Failed", true), (map.ChipWord, map.Note.Length > 0));
        Assert.DoesNotContain("SND-", map.Note, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Retry_is_the_command_to_copy_when_the_catalog_has_no_ready_row_for_it()
    {
        var f = new SetupFixture("extra-approve-all-ready");
        f.Serve("failed");

        var action = Step(await f.Read(Token), "map").Action!;

        Assert.False(action.IsInApp);
        Assert.Equal("sindri context map refresh", action.Cli?.Cli);
    }

    private async Task<SetupViewModel> ReadWith(string scenario)
    {
        _f.Serve(scenario);
        return await _f.Read(Token);
    }
}
