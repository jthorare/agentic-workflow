using System.Net;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The Agents page's states in a real window (agents spec §3, §7): reading as skeleton rows, no agents, no match with a way out, a failed source, an out-of-date list, unavailable and unreadable.</summary>
[Lane(TestLane.Integration)]
public sealed class AgentsPageStatesTests : FrameHost
{
    private async Task<(Window Window, ViewModels.Shell.ShellViewModel Shell)> Open()
    {
        var (window, shell) = await Connected(width: 1440, height: 900);
        shell.GoTo(PageId.Agents);
        return (window, shell);
    }

    [Fact]
    public Task Reading_shows_five_skeleton_rows_and_the_filters_off_until_the_list_arrives() => OnUi(async () =>
    {
        Server.SessionsGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Server.SessionAnswer = RouteAnswer.Api("sessions", "fixture-three-providers");
        var (window, shell) = await Open();

        await Until(window, () => Visible(window, "AgentsSkeleton"));
        Assert.Equal(5, Named<StackPanel>(window, "AgentsSkeleton").Children.OfType<Grid>().Count());
        Assert.Equal(Labels.ReadingSessions, Named<Border>(window, "AgentsReadingName").GetVisualDescendants().OfType<TextBlock>().First().Text);
        Assert.False(Named<ComboBox>(window, "ProviderFilter").IsEnabled);

        Server.SessionsGate.SetResult();
        await Until(window, () => !Visible(window, "AgentsSkeleton") && shell.Agents.Groups.Count > 0);
        Assert.True(Named<ComboBox>(window, "ProviderFilter").IsEnabled);
    });

    [Fact]
    public Task No_agents_says_the_served_sentence_and_offers_the_Workflows_page() => OnUi(async () =>
    {
        Server.SessionAnswer = RouteAnswer.Api("sessions", "empty");
        var (window, shell) = await Open();

        await Until(window, () => shell.Agents.IsEmpty);
        Assert.Equal((string)GoldenFiles.Node("sessions", "empty")["empty"]!, Named<TextBlock>(window, "AgentsEmpty").Text);
        Assert.True(Visible(window, "AgentsOpenWorkflows"));
        Assert.False(Visible(window, "AgentsClearFilters"));
        Assert.False(Visible(window, "AgentsSkeleton"));
        Named<Button>(window, "AgentsOpenWorkflows").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.True(shell.IsWorkflows);
    });

    [Fact]
    public Task Filters_that_match_nothing_say_so_and_Clear_filters_brings_the_list_back() => OnUi(async () =>
    {
        Server.SessionAnswer = RouteAnswer.Api("sessions", "fixture-three-providers");
        var (window, shell) = await Open();
        await Until(window, () => shell.Agents.Groups.Count > 0);

        shell.Agents.FilterState("running");
        shell.Agents.FilterRepo("web");
        await Until(window, () => shell.Agents.IsEmpty);

        Assert.Equal(Labels.NoSessionsMatch, Named<TextBlock>(window, "AgentsEmpty").Text);
        Assert.True(Visible(window, "AgentsClearFilters"));
        Assert.False(Visible(window, "AgentsOpenWorkflows"));
        Named<Button>(window, "AgentsClearFilters").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        await Until(window, () => !shell.Agents.IsEmpty && shell.Agents.Groups.Count > 0);
        Assert.False(Visible(window, "AgentsClearFilters"));
    });

    [Fact]
    public Task A_source_that_failed_adds_an_amber_line_and_keeps_the_list() => OnUi(async () =>
    {
        Server.SessionAnswer = RouteAnswer.Api("sessions", "problems-source-failed");
        var (window, shell) = await Open();

        await Until(window, () => shell.Agents.ProblemLines.Count > 0);
        _ = Assert.Single(shell.Agents.ProblemLines);
        Assert.True(Visible(window, "AgentsProblems"));
        Assert.True(shell.Agents.Groups.Count > 0);
        Assert.False(Visible(window, "AgentsStale"));
    });

    [Fact]
    public Task An_out_of_date_list_stays_and_the_stale_line_says_so() => OnUi(async () =>
    {
        Server.SessionAnswer = RouteAnswer.Api("sessions", "fixture-three-providers");
        var (window, shell) = await Open();
        await Until(window, () => shell.Agents.Groups.Count > 0);

        Server.SessionAnswer = RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-TEST-500", "boom");
        await shell.Agents.Refresh(shell.Health.ApiUrl()!, Token);

        Settle(window);
        Assert.True(Visible(window, "AgentsStale"));
        Assert.Equal(Labels.AgentsOutOfDate, Named<TextBlock>(window, "AgentsStaleText").Text);
        Assert.True(shell.Agents.Groups.Count > 0);
    });

    [Fact]
    public Task Sessions_that_cannot_be_seen_say_why_and_show_no_list() => OnUi(async () =>
    {
        Server.SessionAnswer = RouteAnswer.Problem("sessions", "disabled");
        var (window, shell) = await Open();

        await Until(window, () => shell.Agents.IsUnavailable);
        Assert.Equal(Labels.SessionsUnavailable("disabled"), Named<TextBlock>(window, "AgentsUnavailable").Text);
        Assert.False(Visible(window, "AgentsSkeleton"));
        Assert.False(Visible(window, "AgentsEmpty"));
        Assert.Empty(shell.Agents.Groups);
    });

    [Fact]
    public Task An_answer_this_app_cannot_read_says_so() => OnUi(async () =>
    {
        Server.SessionAnswer = new RouteAnswer(HttpStatusCode.OK, "not json");
        var (window, shell) = await Open();

        await Until(window, () => shell.Agents.IsUnreadable);
        Assert.True(Visible(window, "AgentsUnreadableName"));
        Assert.Equal(Labels.SessionsUnreadable, shell.Agents.UnreadableText);
    });
}
