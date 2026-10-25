using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Setup;
using static Aw.Sindri.Ui.Tests.Setup.SetupFixture;

namespace Aw.Sindri.Ui.Tests.Setup;

/// <summary>The agents step lists Claude Code, Codex and Cursor, each as Connected, Connecting or Not started, from the repo lines the API serves; Connect is the app's own button only when the catalog lets it.</summary>
[Lane(TestLane.Unit)]
public sealed class SetupAgentsTests
{
    private readonly SetupFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SetupStepViewModel Mcp(SetupViewModel setup) => setup.Steps.Single(s => s.Id == "mcp");

    private static (string Agent, string Word)[] Lines(SetupStepViewModel step) => [.. step.Agents.Select(a => (a.Agent, a.ResultText))];

    [Fact]
    public async Task A_done_step_lists_the_three_agents_with_the_ones_the_repo_line_names_connected()
    {
        _f.Serve("all-done");

        var step = Mcp(await _f.Read(Token));

        Assert.Equal([("Claude Code", "Connected"), ("Codex", "Not started"), ("Cursor", "Not started")], Lines(step));
        Assert.True(step.HasAgents);
    }

    [Fact]
    public async Task A_step_that_waits_lists_every_agent_as_not_started()
    {
        _f.Serve("first-run");

        var step = Mcp(await _f.Read(Token));

        Assert.Equal([("Claude Code", "Not started"), ("Codex", "Not started"), ("Cursor", "Not started")], Lines(step));
    }

    [Fact]
    public async Task While_the_step_runs_an_agent_not_yet_reached_is_connecting_and_one_reached_is_connected()
    {
        _f.Serve(WithItems(With("all-done", "mcp", "running"), "mcp", ("demo", "running", "Claude Code, Codex")));

        var step = Mcp(await _f.Read(Token));

        Assert.Equal([("Claude Code", "Connected"), ("Codex", "Connected"), ("Cursor", "Connecting")], Lines(step));
        Assert.Equal(["Done", "Done", "Working"], step.Agents.Select(a => a.Chip.Word), StringComparer.Ordinal);
    }

    [Fact]
    public async Task With_two_repos_an_agent_is_connected_only_when_every_repo_has_it()
    {
        _f.Serve(WithItems(Golden("all-done"), "mcp", ("demo", "done", "Claude Code, Codex"), ("other", "done", "Claude Code")));

        var step = Mcp(await _f.Read(Token));

        Assert.Equal([("Claude Code", "Connected"), ("Codex", "Not started"), ("Cursor", "Not started")], Lines(step));
    }

    [Fact]
    public async Task Served_provider_states_set_each_agent_and_a_failed_one_says_failed()
    {
        _f.Serve(WithProviders(WithItems(Golden("all-done"), "mcp", ("demo", "failed", null)), "mcp", "demo", ("claude", "connected", null), ("codex", "failed", "no write access"), ("cursor", "connecting", null)));

        var step = Mcp(await _f.Read(Token));

        Assert.Equal([("Claude Code", "Connected"), ("Codex", "Failed"), ("Cursor", "Connecting")], Lines(step));
        Assert.Equal(["Done", "Failed", "Working"], step.Agents.Select(a => a.Chip.Word), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_provider_the_repo_lines_do_not_list_is_not_started_even_when_the_detail_names_it()
    {
        _f.Serve(WithProviders(WithItems(Golden("all-done"), "mcp", ("demo", "done", "Claude Code, Codex")), "mcp", "demo", ("claude", "connected", null)));

        var step = Mcp(await _f.Read(Token));

        Assert.Equal([("Claude Code", "Connected"), ("Codex", "Not started"), ("Cursor", "Not started")], Lines(step));
    }

    [Fact]
    public async Task With_two_repos_a_provider_is_connected_only_when_every_repo_lists_it_connected()
    {
        _f.Serve(WithProviders(WithProviders(WithItems(Golden("all-done"), "mcp", ("demo", "done", null), ("other", "running", null)), "mcp", "demo", ("claude", "connected", null)), "mcp", "other", ("claude", "connecting", null)));

        var step = Mcp(await _f.Read(Token));

        Assert.Equal(("Claude Code", "Connecting"), Lines(step)[0]);
    }

    [Fact]
    public async Task Each_line_is_named_for_assistive_technology_with_its_result_and_the_fixed_lines_are_the_specs()
    {
        _f.Serve("all-done");

        var step = Mcp(await _f.Read(Token));

        Assert.Equal(["Claude Code, connected", "Codex, not started", "Cursor, not started"], step.Agents.Select(a => a.Name), StringComparer.Ordinal);
        Assert.Equal(("Codex and Cursor will each ask you to trust the folder once.", "Only this repository, never your whole account. Skipped automatically if a coding agent isn't installed."), (step.TrustOnceText, step.AgentsNoteText));
    }

    [Fact]
    public async Task No_other_step_lists_agents()
    {
        _f.Serve("all-done");

        var setup = await _f.Read(Token);

        Assert.All(setup.Steps.Where(s => s.Id != "mcp"), s => Assert.False(s.HasAgents));
    }

    [Fact]
    public async Task A_failed_agents_step_offers_Connect_your_agents_as_the_apps_own_button_and_one_press_registers_once()
    {
        _f.Serve(With("all-done", "mcp", "failed", "RegisterMcp"));
        var setup = await _f.Read(Token);
        var step = Mcp(setup);
        var action = step.Action!;

        await setup.RegisterMcp(Token);
        await setup.RegisterMcp(Token);   // the button is gone while it works, so a second press does nothing

        Assert.Equal(("Connect your agents", true, false), (action.Label, action.IsInApp, step.HasAction));
        var call = Assert.Single(_f.Server.RegisteredMcp);
        Assert.Equal((null, "Working", Labels.SetupMcpAgain), (call.Repo, step.ChipWord, step.Note));
    }

    [Fact]
    public async Task Connect_your_agents_is_the_command_to_copy_when_the_catalog_has_no_ready_row_for_it()
    {
        var f = new SetupFixture("extra-approve-all-ready");   // this catalog has no RegisterMcp row (the served one is ready since api #612)
        f.Serve(With("all-done", "mcp", "failed", "RegisterMcp"));

        var action = Mcp(await f.Read(Token)).Action!;

        Assert.Equal((false, "sindri mcp register --apply"), (action.IsInApp, action.Cli?.Cli));
    }

    [Fact]
    public async Task A_refused_Connect_is_said_in_words_and_the_step_stays_failed()
    {
        _f.Serve(With("all-done", "mcp", "failed", "RegisterMcp"));
        _f.Server.RegisterMcpAnswer = RouteAnswer.Problem("setup-mcp", "not-approved");
        var setup = await _f.Read(Token);

        await setup.RegisterMcp(Token);

        var step = Mcp(setup);
        Assert.Equal(("Failed", true), (step.ChipWord, step.Note.Length > 0));
        Assert.DoesNotContain("SND-", step.Note, StringComparison.Ordinal);
    }
}
