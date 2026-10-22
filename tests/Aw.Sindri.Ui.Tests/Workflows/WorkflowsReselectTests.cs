using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The Workflows selection across re-reads (screens §4.2, U8 T7; review run 1, V2): kept by address, moved to the row that followed a row that went, and focus asked for only when it was lost.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsReselectTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static IReadOnlyList<InstanceSummary> Served => FixtureInstances.List("open").Instances;

    private static InstanceSummary Desk => Served.First(r => r.Answer is null && r.Actions.Contains("CompleteStep", StringComparer.Ordinal) && r.States.Any(s => s.Desk && string.Equals(s.State, r.State, StringComparison.Ordinal)));

    private static InstanceSummary Locked => Served.First(r => r.Answer is not null && string.Equals(r.Answer.Class, "protected", StringComparison.Ordinal));

    private async Task<ShellViewModel> OnWorkflows()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal && shell.Workflows.Groups.Count > 0, Token);
        shell.GoTo(PageId.Workflows);
        shell.Workflows.SetViewMode(WorkflowViewMode.List);   // the grouped list: the keys walk its groups
        return shell;
    }

    private static List<string> Flat(ShellViewModel shell) => [.. shell.Workflows.Groups.SelectMany(g => g.Rows).Select(r => r.Address)];

    // The list the API would serve with its instances reordered or reduced.
    private void Serve(Func<IEnumerable<JsonNode?>, IEnumerable<JsonNode?>> change)
    {
        var body = GoldenFiles.Node("workflow-instances", "open");
        var rows = change([.. body["instances"]!.AsArray()]).Select(r => r?.DeepClone());
        body["instances"] = new JsonArray([.. rows]);
        _server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());
    }

    [Fact]
    public Task Selection_is_kept_by_address_across_refreshes() => UiThread.Run(async () =>
    {
        var shell = await OnWorkflows();
        var keep = Flat(shell)[3];
        Assert.True(shell.Workflows.Select(keep));

        Serve(rows => rows.Reverse());
        await shell.Workflows.Refresh(_api, Token);

        Assert.Equal(keep, shell.Workflows.SelectedAddress);
        Expect.Sequence([keep], shell.Workflows.Groups.SelectMany(g => g.Rows).Where(r => r.IsSelected).Select(r => r.Address));
    }, Token);

    [Fact]
    public Task Keys_never_act_on_a_row_that_went_or_that_the_user_left() => UiThread.Run(async () =>
    {
        var shell = await OnWorkflows();
        Assert.True(shell.Workflows.Select(Desk.Address));
        var left = shell.Workflows.SelectedRow!;
        Assert.True(shell.Workflows.Select(Locked.Address));
        Assert.Equal((false, true), (left.IsSelected, shell.Workflows.SelectedRow!.IsSelected));

        Serve(rows => rows.Where(r => !string.Equals((string)r!["address"]!, Locked.Address, StringComparison.Ordinal)));
        await shell.Workflows.Refresh(_api, Token);

        Assert.NotEqual(Locked.Address, shell.Workflows.SelectedAddress, StringComparer.Ordinal);
        Assert.False(shell.IsSheetOpen);
    }, Token);

    [Fact]
    public Task The_selected_row_going_selects_the_row_that_followed_it() => UiThread.Run(async () =>
    {
        var shell = await OnWorkflows();
        var flat = Flat(shell);
        Assert.True(shell.Workflows.Select(flat[3]));

        Serve(rows => rows.Where(r => !string.Equals((string)r!["address"]!, flat[3], StringComparison.Ordinal)));
        await shell.Workflows.Refresh(_api, Token);

        Assert.Equal(flat[4], shell.Workflows.SelectedAddress, StringComparer.Ordinal);
        Expect.Sequence([flat[4]], shell.Workflows.Groups.SelectMany(g => g.Rows).Where(r => r.IsSelected).Select(r => r.Address));
    }, Token);

    [Fact]
    public Task The_last_row_going_selects_the_row_before_it() => UiThread.Run(async () =>
    {
        var shell = await OnWorkflows();
        var flat = Flat(shell);
        Assert.True(shell.Workflows.Select(flat[^1]));

        Serve(rows => rows.Where(r => !string.Equals((string)r!["address"]!, flat[^1], StringComparison.Ordinal)));
        await shell.Workflows.Refresh(_api, Token);

        Assert.Equal(flat[^2], shell.Workflows.SelectedAddress, StringComparer.Ordinal);
    }, Token);

    [Fact]
    public Task A_read_asks_for_focus_on_the_selection_only_if_it_was_lost() => UiThread.Run(async () =>
    {
        var shell = await OnWorkflows();
        var asked = new List<(string Address, bool OnlyIfLost)>();
        shell.Workflows.FocusRequested += (_, e) => asked.Add((e.Address, e.OnlyIfLost));
        var keep = Flat(shell)[3];
        Assert.True(shell.Workflows.Select(keep));

        await shell.Workflows.Refresh(_api, Token);

        Assert.Equal([(keep, true)], asked);
    }, Token);
}
