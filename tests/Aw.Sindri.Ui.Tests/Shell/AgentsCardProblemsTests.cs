using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Sessions;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>
/// The System page's Agents card says what its problems are (#4012): each one's sentence and code under the counts, and a link to the Agents page.
/// While the owner turned session watching off it says so in place of problems (#4316).
/// </summary>
[Lane(TestLane.Unit)]
public sealed class AgentsCardProblemsTests
{
    private const string Failed = "The claude-records source couldn't be read in the last pass";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_counted_problem_shows_its_sentence_and_code_with_the_link()
    {
        var system = new SystemTestHarness();
        var rows = new[] { new SessionProblemView("claude-records", "SND-SESSION-001", Failed) };

        await system.Show(A.Status() with { Sessions = new SessionCountsView(Live: 3, Asking: 1, Problems: 1, Running: 1, ProblemRows: rows) });

        Assert.Equal([new AgentProblemLine(Failed, "SND-SESSION-001")], system.Vm.AgentsProblemLines);
        Assert.True(system.Vm.HasAgentsProblems);
    }

    [Fact]
    public async Task No_problem_keeps_the_card_as_it_was()
    {
        var system = new SystemTestHarness();

        await system.Show(A.Status() with { Sessions = new SessionCountsView(Live: 3, Asking: 1, Problems: 0, Running: 1, ProblemRows: []) });

        Assert.Empty(system.Vm.AgentsProblemLines);
        Assert.False(system.Vm.HasAgentsProblems);
    }

    [Fact]
    public async Task An_older_answer_with_a_count_and_no_rows_still_shows_the_link()
    {
        var system = new SystemTestHarness();

        await system.Show(A.Status() with { Sessions = new SessionCountsView(Live: 3, Asking: 1, Problems: 2) });

        Assert.Empty(system.Vm.AgentsProblemLines);
        Assert.True(system.Vm.HasAgentsProblems);
    }

    [Fact]
    public async Task The_agents_card_shows_watching_off_in_place_of_problems()
    {
        var system = new SystemTestHarness();

        await system.Show(A.Status() with { Sessions = new SessionCountsView(Live: 0, Asking: 0, Problems: 0, Running: 0, ProblemRows: [], Watching: "off") });

        Assert.True(system.Vm.IsAgentsWatchingOff);
        Assert.Equal("Session watching is off · turn it on with sessions.enabled in launcher.json, then restart Sindri", system.Vm.AgentsWatchingOffText);
        Assert.Contains(new HealthCell(Labels.AgentsProblems, "0"), system.Vm.AgentsRows);
        Assert.False(system.Vm.HasAgentsProblems);
    }

    [Theory]
    [InlineData("on")]
    [InlineData(null)]
    public async Task Watching_on_or_an_older_answer_shows_no_watching_line(string? watching)
    {
        var system = new SystemTestHarness();

        await system.Show(A.Status() with { Sessions = new SessionCountsView(Live: 3, Asking: 1, Problems: 0, Running: 1, ProblemRows: [], Watching: watching) });

        Assert.False(system.Vm.IsAgentsWatchingOff);
    }

    [Fact]
    public void The_agents_page_says_watching_is_off_in_the_same_words() =>
        Assert.Equal(Labels.AgentsWatchingOff, Labels.SessionsUnavailable("disabled"));

    [Fact]
    public Task The_link_opens_the_agents_page() => UiThread.Run(async () =>
    {
        var shell = new ShellViewModel(new InMemoryEndpointRecords(), new FakeSindriApi(), new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);

        shell.Health.RequestAgents();

        Assert.Equal(PageId.Agents, shell.CurrentPage);
    }, Token);
}
