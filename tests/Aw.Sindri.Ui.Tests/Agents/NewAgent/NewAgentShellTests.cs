using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Agents.Page;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Agents;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Agents.NewAgent;

/// <summary>The New agent form in the shell (agents spec §15.4, §15.6; #3384): N opens it with the profile's repos and the last start, Esc keeps the text, and a 2xx selects the new row and asks for its pane.</summary>
[Lane(TestLane.Unit)]
public sealed class NewAgentShellTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { NeedsYouView = RouteAnswer.Api("needs-you", "empty"), SetupAnswer = RouteAnswer.SpecShaped("setup", "extra-priority-mix") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> OnAgents()
    {
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _records.Write(A.Record());
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal, Token);
        shell.GoTo(PageId.Agents);
        return shell;
    }

    [Fact]
    public Task N_on_the_Agents_page_opens_the_form_with_the_profiles_repos_and_the_last_agent() => UiThread.Run(async () =>
    {
        _server.LastStartAnswer = new(Repo: null, Agent: "codex");
        var shell = await OnAgents();

        var used = shell.Press("N");
        await ShellWait.Until(shell, () => shell.NewAgent.Repos.Count > 0, Token);   // the form is open at once; the repos land after

        Assert.True(used);
        Assert.True(shell.NewAgent.IsOpen);
        Assert.Equal("codex", shell.NewAgent.Agent.Id);
    }, Token);

    [Fact]
    public Task Esc_closes_the_form_and_N_brings_back_the_same_text() => UiThread.Run(async () =>
    {
        var shell = await OnAgents();
        await shell.OpenNewAgent(Token);
        shell.NewAgent.Prompt = "fix the flaky test";

        _ = await shell.NewAgent.Press("Escape", ctrl: false, shift: false, Token);
        await shell.OpenNewAgent(Token);

        Assert.Equal((true, "fix the flaky test"), (shell.NewAgent.IsOpen, shell.NewAgent.Prompt));
    }, Token);

    [Fact]
    public Task A_2xx_asks_for_the_new_sessions_pane_and_selects_its_row() => UiThread.Run(async () =>
    {
        var shell = await OnAgents();
        await shell.OpenNewAgent(Token);
        AgentStartedViewRef? asked = null;
        shell.AgentPaneRequested += (_, e) => asked = e.Started;

        await shell.NewAgent.Start(background: false, Token);

        var sent = Assert.Single(_server.Starts);
        Assert.Equal(("sindri-" + sent.ActId, sent.ActId), (asked?.Id, asked?.ActId));
    }, Token);

    [Fact]
    public Task A_2xx_selects_the_started_row_before_and_after_its_id_changes_3384() => UiThread.Run(async () =>
    {
        var shell = await OnAgents();
        await shell.OpenNewAgent(Token);
        await shell.NewAgent.Start(background: false, Token);
        var act = Assert.Single(_server.Starts).ActId;
        var url = shell.Health.ApiUrl()!;

        // Before the fleet file: the Starting row's id is the tmux session name.
        var starting = AgentsPageFixture.Edited(list => Retarget(list, $"claude-code/sindri-{act}", $"sindri-{act}", act));
        _server.SessionAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, starting.ToJsonString());
        await shell.Agents.Refresh(url, Token);
        var before = shell.Agents.SelectedKey;

        // After it: the same session under the provider's own id.
        var found = AgentsPageFixture.Edited(list => Retarget(list, "claude-code/cccccccc-0000-4000-8000-000000000009", "cccccccc-0000-4000-8000-000000000009", act));
        _server.SessionAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, found.ToJsonString());
        await shell.Agents.Refresh(url, Token);

        Assert.Equal(($"claude-code/sindri-{act}", "claude-code/cccccccc-0000-4000-8000-000000000009"), (before, shell.Agents.SelectedKey));
    }, Token);

    // The first row becomes the started session: its address, id and tmux session name.
    private static void Retarget(System.Text.Json.Nodes.JsonObject list, string reference, string id, string act)
    {
        var row = list["sessions"]![0]!.AsObject();
        row["ref"] = reference;
        row["id"] = id;
        row["host"]!["session"] = "sindri-" + act;
    }
}
