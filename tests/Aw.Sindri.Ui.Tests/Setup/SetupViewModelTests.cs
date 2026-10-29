using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Setup;
using static Aw.Sindri.Ui.Tests.Setup.SetupFixture;

namespace Aw.Sindri.Ui.Tests.Setup;

/// <summary>The setup checklist as Settings reads it: steps in the app's own titles and the five state words, the rows that open by themselves, the progress strip, Home's one line, and what is shown for a list that can't be read.</summary>
[Lane(TestLane.Unit)]
public sealed class SetupViewModelTests
{
    private readonly SetupFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string[] Ids(IEnumerable<SetupStepViewModel> steps) => [.. steps.Select(s => s.Id)];

    private static SetupStepViewModel Step(SetupViewModel setup, string id) => setup.Steps.Single(s => s.Id == id);

    [Fact]
    public async Task Map_row_has_no_button_unless_it_failed()
    {
        foreach (var scenario in (string[])["first-run", "running-progress", "map-retrying", "all-done"])
        {
            Assert.False(Step(await ReadWith(scenario), "map").HasAction, scenario);   // waiting, building, retrying by itself, done: the map builds itself
        }

        var failed = Step(await ReadWith("failed"), "map");
        var building = Step(await ReadWith("running-progress"), "map");
        Assert.Equal((true, "Retry"), (failed.HasAction, failed.Action?.Label));
        Assert.Equal((true, 0.4, "40 of 100 files", false), (building.HasProgress, building.ProgressFraction, building.ProgressText, building.IsMeterFailed));   // a meter, not a button
        Assert.Equal(Labels.SetupMapRetries, failed.HintText);   // and a line that it also retries on its own
        Assert.Equal(string.Empty, building.HintText);
    }

    [Fact]
    public async Task A_failed_map_that_counted_shows_where_it_stopped_on_a_meter_in_the_error_colour()
    {
        _f.Serve(WithProgress(Golden("failed"), "map", 40, 100, "files"));

        var map = Step(await _f.Read(Token), "map");

        Assert.Equal((true, true, 0.4, "40 of 100 files"), (map.HasProgress, map.IsMeterFailed, map.ProgressFraction, map.ProgressText));
    }

    [Fact]
    public async Task A_web_address_in_a_steps_words_becomes_a_link_and_leaves_the_shown_text()
    {
        _f.Serve(WithExtraStep(Golden("first-run"), "sources", "Connect your sources", "todo", "Install Docker to set up sources https://docs.docker.com/get-docker/"));
        var sources = Step(await _f.Read(Token), "sources");
        var map = Step(await ReadWith("failed"), "map");

        Assert.Equal(("Install Docker to set up sources", true, "https://docs.docker.com/get-docker/", "Open docs.docker.com"), (sources.Sub, sources.HasLink, sources.LinkUri?.AbsoluteUri, sources.LinkText));
        Assert.DoesNotContain("https://", sources.Name, StringComparison.Ordinal);
        Assert.Equal((false, null), (map.HasLink, map.LinkUri));
    }

    [Fact]
    public async Task A_skipped_step_shows_a_dash_not_the_done_tick()
    {
        var skipped = Step(await ReadWith("first-run", "map", "skipped"), "map");
        var done = Step(await ReadWith("all-done"), "map");

        Assert.Equal(("Skipped", "–"), (skipped.ChipWord, skipped.NumberText));
        Assert.Equal("✓", done.NumberText);
    }

    [Fact]
    public async Task A_served_step_with_no_form_here_is_an_ordinary_row_with_its_command_to_copy()
    {
        _f.ListCliAction("ConnectSources", "sindri index connect github");
        _f.Serve(WithExtraStep(Golden("first-run"), "sources", "Connect your sources", "pending", action: "ConnectSources"));

        var sources = Step(await _f.Read(Token), "sources");
        await sources.Action!.Cli!.Copy(Token);

        Assert.Equal(("Connect your sources", "To do", "pending"), (sources.Title, sources.ChipWord, sources.Chip.Word));
        Assert.Equal((false, null, null, null), (sources.Action.IsInApp, sources.Action.Protected, sources.Repo, sources.Profile));
        Assert.Equal("sindri index connect github", _f.Clipboard.Text);
    }

    [Fact]
    public async Task First_run_opens_the_first_step_and_shows_to_do_for_the_rest()
    {
        _f.Serve("first-run");

        var setup = await _f.Read(Token);

        Assert.Equal(["repo", "profile", "recipes", "mcp", "map"], Ids(setup.Steps.Take(5)));
        Assert.Equal(["Needs you", "To do", "To do", "To do", "To do"], setup.Steps.Take(5).Select(s => s.ChipWord), StringComparer.Ordinal);
        Assert.Equal([true, false, false, false, false], setup.Steps.Take(5).Select(s => s.IsOpen));
        Assert.Equal(("Set up Sindri", Labels.SetupCount(0, setup.Steps.Count), true), (setup.Lead, setup.CountText, setup.HasSub));
    }

    [Fact]
    public async Task The_steps_carry_the_apps_own_titles_in_order()
    {
        _f.Serve("first-run");

        var setup = await _f.Read(Token);

        Assert.Equal(
            ["Add your repository", "Approve your settings", "Approve the workflows", "Connect your coding agents", "Build the product map"],
            setup.Steps.Take(5).Select(s => s.Title),
            StringComparer.Ordinal);
        Assert.Equal([1, 2, 3, 4, 5], setup.Steps.Take(5).Select(s => s.Number));
    }

    [Fact]
    public async Task Each_state_reads_as_one_of_the_five_words_and_a_step_this_build_does_not_know_keeps_its_own_word()
    {
        _f.Serve(WithExtraStep(Golden("failed"), "sources", "Connect your sources", "paused"));

        var setup = await _f.Read(Token);

        Assert.Equal(["Done", "Done", "Done", "Done", "Failed", "To do"], [.. setup.Steps.Select(s => s.ChipWord).Take(5), Step(setup, "sources").ChipWord], StringComparer.Ordinal);
        Assert.Equal("paused", Step(setup, "sources").Chip.Word);   // unknown: the To do chip, with the served word
        Assert.Equal("Working", Step(await ReadWith("running-progress"), "map").ChipWord);
        Assert.Equal("Skipped", Step(await ReadWith("first-run", "map", "skipped"), "map").ChipWord);
    }

    private async Task<SetupViewModel> ReadWith(string scenario)
    {
        _f.Serve(scenario);
        return await _f.Read(Token);
    }

    private async Task<SetupViewModel> ReadWith(string scenario, string step, string state)
    {
        _f.Serve(With(scenario, step, state));
        return await _f.Read(Token);
    }

    [Fact]
    public async Task A_step_that_needs_you_or_failed_opens_by_itself_and_the_others_stay_closed()
    {
        var failed = await ReadWith("failed");
        var profile = await ReadWith("profile-needs-approval");

        Assert.Equal(["map"], Ids(failed.Steps.Where(s => s.IsOpen)), StringComparer.Ordinal);
        Assert.Equal(["profile"], Ids(profile.Steps.Where(s => s.IsOpen)), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Opening_a_row_never_closes_another_and_the_persons_choice_survives_a_read()
    {
        var setup = await ReadWith("profile-needs-approval");
        Step(setup, "recipes").IsOpen = true;
        Step(setup, "profile").IsOpen = false;

        await setup.Refresh(Url, Token);

        Assert.Equal(["recipes"], Ids(setup.Steps.Where(s => s.IsOpen)), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_step_that_moves_into_needs_you_opens_again_without_closing_the_others()
    {
        var setup = await ReadWith("running-progress");
        Step(setup, "repo").IsOpen = true;
        _f.Serve(With("running-progress", "map", "failed"));

        await setup.Refresh(Url, Token);

        Assert.Equal(["repo", "map"], Ids(setup.Steps.Where(s => s.IsOpen)), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_done_row_shows_what_it_did_and_a_waiting_row_why()
    {
        var setup = await ReadWith("failed");

        Assert.Equal("26 workflows.", Step(setup, "recipes").Sub);   // done: the served detail
        Assert.Equal("The last try at building the map didn't finish.", Step(setup, "map").ErrorText);
        Assert.Equal("Waits for a repository.", Step(await ReadWith("first-run"), "profile").Sub);
    }

    [Fact]
    public async Task The_progress_strip_has_one_segment_per_step_done_working_or_open()
    {
        var done = await ReadWith("running-progress");
        var first = await ReadWith("first-run");

        Assert.Equal(["done", "done", "done", "done", "working"], done.Segments.Take(5).Select(s => s.Kind), StringComparer.Ordinal);
        Assert.Equal(Labels.SetupCount(done.Steps.Count(s => s.IsFinished), done.Steps.Count), done.CountText);
        Assert.Equal(["todo", "todo", "todo", "todo", "todo"], first.Segments.Take(5).Select(s => s.Kind), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Homes_meter_counts_finished_steps_out_of_all_of_them()
    {
        var first = await ReadWith("first-run");
        var progress = await ReadWith("running-progress");

        Assert.Equal(((0.0, (double)first.Steps.Count), ((double)(progress.Steps.Count - 1), (double)progress.Steps.Count)), ((first.MeterDone, first.MeterMax), (progress.MeterDone, progress.MeterMax)));
    }

    [Fact]
    public async Task Home_shows_one_line_while_any_step_is_open_and_none_when_setup_is_finished()
    {
        var open = await ReadWith("profile-needs-approval");
        var finished = await ReadWith("all-done");
        var skipped = await ReadWith("all-done", "map", "skipped");

        Assert.Equal((true, "Finish setting up Sindri"), (open.IsHomeLineShown, open.HomeLineText));
        Assert.Equal((false, false), (skipped.IsHomeLineShown, finished.IsHomeLineShown));   // skipped counts as finished
        Assert.Equal(("Sindri is set up", Labels.SetupCount(finished.Steps.Count, finished.Steps.Count), false), (finished.Lead, finished.CountText, finished.HasSub));
    }

    [Fact]
    public async Task Home_line_names_the_first_step_still_open_and_Continue_opens_its_row_and_asks_for_the_checklist()
    {
        var setup = await ReadWith("profile-needs-approval");
        var asked = 0;
        setup.ChecklistRequested += (_, _) => asked++;
        Step(setup, "profile").IsOpen = false;

        Assert.Equal(("Approve your settings", "Continue", "Finish setting up Sindri, 1 of 6 done. Next: Approve your settings"), (setup.HomeStepText, setup.ContinueText, setup.HomeLineName));
        setup.Continue();

        Assert.Equal((1, true), (asked, Step(setup, "profile").IsOpen));
    }

    [Fact]
    public async Task A_step_that_reopens_brings_the_line_back()
    {
        var setup = await ReadWith("all-done");
        _f.Serve(With("all-done", "map", "running"));

        await setup.Refresh(Url, Token);

        Assert.Equal((false, true), (setup.IsFinished, setup.IsHomeLineShown));
    }

    [Fact]
    public async Task The_line_opens_the_checklist()
    {
        var setup = await ReadWith("first-run");
        var asked = 0;
        setup.ChecklistRequested += (_, _) => asked++;

        setup.RequestChecklist();

        Assert.Equal(1, asked);
    }

    [Fact]
    public async Task Ids_and_codes_wait_in_Details_and_never_in_the_words_up_front()
    {
        var map = Step(await ReadWith("failed"), "map");

        Assert.DoesNotContain("SND-", map.Name + map.Sub + map.ErrorText + map.Chip.Word, StringComparison.Ordinal);
        Assert.Contains(map.Fields, f => f is ("Error code", "SND-CONTEXT-042"));
        Assert.Contains(map.Fields, f => f is ("Step", "map"));
    }

    [Fact]
    public async Task A_counted_step_shows_how_far_it_is_and_a_repo_step_one_line_per_repo()
    {
        var map = Step(await ReadWith("running-progress"), "map");

        Assert.Equal(("Working", "40 of 100 files", 0.4), (map.ChipWord, map.ProgressText, map.ProgressFraction));
        var line = Assert.Single(map.Items);
        Assert.Equal(("demo", "Working"), (line.Repo, line.ChipText));
    }

    [Fact]
    public async Task A_repeat_read_keeps_each_step_and_changes_it_in_place()
    {
        var setup = await ReadWith("first-run");
        var repo = setup.Steps[0];
        _f.Serve("profile-needs-approval");

        await setup.Refresh(Url, Token);

        Assert.Same(repo, setup.Steps[0]);
        Assert.Equal("Done", repo.ChipWord);
    }

    [Fact]
    public async Task A_change_after_the_first_read_is_announced_once_in_words()
    {
        var setup = await ReadWith("first-run");
        Assert.False(setup.Live.IsAnnouncing);

        _f.Serve("profile-needs-approval");
        await setup.Refresh(Url, Token);

        Assert.Equal((true, "Add your repository: done. Approve your settings: needs you"), (setup.Live.IsAnnouncing, setup.Live.Announcement));
    }

    [Fact]
    public async Task The_first_read_announces_nothing() => Assert.False((await ReadWith("failed")).Live.IsAnnouncing);

    [Fact]
    public async Task Before_the_first_answer_the_checklist_is_reading()
    {
        _f.Serve("first-run");
        var setup = _f.Setup();

        Assert.Equal((true, false, false), (setup.IsReading, setup.HasSteps, setup.IsHomeLineShown));
        await setup.Refresh(Url, Token);
        Assert.Equal((false, true), (setup.IsReading, setup.HasSteps));
    }

    [Fact]
    public async Task A_sindri_without_the_route_says_setup_is_not_in_this_build_and_Home_shows_no_line()
    {
        _f.Server.SetupAnswer = RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-API-004", "The API answered without an error code");

        var setup = await _f.Read(Token);

        Assert.Equal((true, false, false), (setup.IsNotServed, setup.IsHomeLineShown, setup.IsReading));
        Assert.Equal("Setup isn't available in this build yet.", setup.NotServedText);
    }

    [Fact]
    public async Task A_404_after_steps_were_shown_drops_the_stale_rows_and_shows_only_the_not_available_text()
    {
        var setup = await ReadWith("first-run");
        _f.Server.SetupAnswer = RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-API-004", "The API answered without an error code");

        await setup.Refresh(Url, Token);

        Assert.Equal((true, 0, false, false), (setup.IsNotServed, setup.Steps.Count, setup.HasSteps, setup.IsHomeLineShown));
    }

    [Fact]
    public async Task A_list_in_a_shape_this_build_cannot_read_is_unreadable()
    {
        _f.Server.SetupAnswer = new RouteAnswer(HttpStatusCode.OK, "{\"steps\":\"nope\"}");

        var setup = await _f.Read(Token);

        Assert.Equal((true, false), (setup.IsUnreadable, setup.IsHomeLineShown));
    }

    [Fact]
    public async Task When_nothing_answers_the_checklist_says_sindri_is_not_running_with_the_launcher_to_copy()
    {
        _f.Server.GoDown();

        var setup = await _f.Read(Token);

        Assert.Equal((true, false, false), (setup.IsNotRunning, setup.IsFailed, setup.IsHomeLineShown));
        Assert.Equal(("Sindri isn't running.", "sindri", "The app reconnects on its own when Sindri starts."), (setup.NotRunningText, setup.Launcher.Cli, setup.ReconnectsText));
        await setup.Launcher.Copy(Token);
        Assert.Equal("sindri", _f.Clipboard.Text);
    }

    [Fact]
    public async Task A_failed_first_read_says_why_and_a_retry_reads_again()
    {
        _f.Server.SetupAnswer = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-005", "a setup source failed");
        var setup = await _f.Read(Token);
        Assert.Equal((true, false), (setup.IsFailed, setup.IsNotRunning));
        Assert.StartsWith("Couldn't read setup just now.", setup.FailedText, StringComparison.Ordinal);

        _f.Serve("first-run");
        await setup.Retry(Token);

        Assert.Equal((false, 6), (setup.IsFailed, setup.Steps.Count));
    }

    [Fact]
    public async Task A_failed_later_read_keeps_the_steps_and_marks_them_stale()
    {
        var setup = await ReadWith("first-run");
        _f.Server.GoDown();

        await setup.Refresh(Url, Token);

        Assert.Equal((6, true, false), (setup.Steps.Count, setup.IsStale, setup.IsFailed));
    }
}
