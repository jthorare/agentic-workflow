using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Views.Agents;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The Agents table in a real window (agents spec §2, §7): the groups in order with their counts, the six columns under their heads, and the compact band under 640 px.</summary>
[Lane(TestLane.Integration)]
public sealed class AgentsPageRowsTests : FrameHost
{
    private async Task<(Window Window, ViewModels.Shell.ShellViewModel Shell)> Shown(double width)
    {
        Server.SessionAnswer = RouteAnswer.Api("sessions", "fixture-three-providers");
        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.Agents);
        await Until(window, () => shell.Agents.Groups.Count > 0);
        return (window, shell);
    }

    private static List<string> Heads(Window window) =>
        [.. Named<Grid>(window, "AgentsColumnsGrid").Children.OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text ?? string.Empty)];

    [Fact]
    public Task The_groups_come_in_order_with_their_counts_and_the_six_columns_sit_under_their_heads() => OnUi(async () =>
    {
        var (window, shell) = await Shown(1440);

        Assert.Equal(["Started by Sindri (1)", "Not part of a workflow (2)"], shell.Agents.Groups.Select(g => g.Header), StringComparer.Ordinal);
        Assert.Equal(["Status", "Agent", "Repository", "Where it runs", "What it is doing", "For"], Heads(window), StringComparer.Ordinal);
        Assert.False(shell.Agents.IsCompact);
        var repos = window.GetVisualDescendants().OfType<SessionRow>().Select(r => Named<Border>(r, "SessionRowRepo")).Where(b => b.IsEffectivelyVisible).Select(b => Avalonia.Automation.AutomationProperties.GetName(b)).ToList();
        Assert.Contains("api · ideas", repos, StringComparer.Ordinal);
        Assert.Contains("web", repos, StringComparer.Ordinal);
    });

    [Fact]
    public Task Under_640_px_only_status_agent_and_for_stay() => OnUi(async () =>
    {
        var (window, shell) = await Shown(1440);

        window.GetVisualDescendants().OfType<AgentsPage>().Single().Width = 600;   // the window keeps a minimum width: the page region is narrowed instead
        await Until(window, () => shell.Agents.IsCompact);
        Assert.Equal(["Status", "Agent", "For"], Heads(window), StringComparer.Ordinal);
        Assert.All(window.GetVisualDescendants().OfType<SessionRow>(), r =>
        {
            Assert.False(Named<Border>(r, "SessionRowRepo").IsEffectivelyVisible);
            Assert.False(Named<Border>(r, "SessionRowWhere").IsEffectivelyVisible);
            Assert.True(Named<Border>(r, "SessionRowSince").IsEffectivelyVisible);
            Assert.Equal(0, Named<Grid>(r, "SessionRowGrid").ColumnDefinitions[2].Width.Value);
        });
    });

    [Fact]
    public Task A_session_with_no_repository_shows_the_dash_in_the_Repository_column() => OnUi(async () =>
    {
        var (window, _) = await Shown(1920);

        var repos = window.GetVisualDescendants().OfType<SessionRow>().Select(r => Named<Border>(r, "SessionRowRepo")).Where(b => b.IsEffectivelyVisible).Select(b => Avalonia.Automation.AutomationProperties.GetName(b)).ToList();
        Assert.Equal(3, repos.Count);
        Assert.DoesNotContain(string.Empty, repos, StringComparer.Ordinal);
        Assert.Contains("—", repos, StringComparer.Ordinal);
    });
}
