using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The strip's re-reads (U3' plan "Re-query map", S8): every event re-reads it, whatever page is visible, without moving anything the human holds.</summary>
[Lane(TestLane.Unit)]
public sealed class StripRequeryTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // The root golden as served, less its first row (the incident): the list before the incident starts.
    private static RouteAnswer WithoutTheFirstRow()
    {
        var body = (JsonObject)JsonNode.Parse(FixtureBody())!; // why: the golden is a JSON object
        var rows = body["instances"]!.AsArray();
        rows.RemoveAt(0);
        return new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());
    }

    private static string FixtureBody() => GoldenFiles.Api("workflow-instances", "root");

    private async Task<ShellViewModel> Connected()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal && _server.InstancesCalls > 0 && shell.Strip.Rows.Count > 0, Token);
        return shell;
    }

    [Theory]
    [InlineData("Home")]
    [InlineData("Settings")]
    public Task Workflow_events_reread_the_strip_and_needs_you_whatever_page_is_visible(string page) => UiThread.Run(async () =>
    {
        var shell = await Connected();
        shell.GoTo(Enum.Parse<PageId>(page));
        var (strip, needsYou, reads) = (_server.InstancesCalls, _server.NeedsYouCalls, shell.Health.ViewReads);

        _server.Push(A.Event(70, "workflow.started"));

        await ShellWait.Until(shell, () => shell.Health.ViewReads > reads, Token);
        Assert.True(_server.InstancesCalls > strip && _server.NeedsYouCalls > needsYou);
    }, Token);

    // The strip asks for what the Workflows page asks for: the served open filter decides who is open (a cancelled root included), never a chip word.
    [Fact]
    public Task The_strip_asks_for_open_roots_so_the_server_decides_who_is_open() => UiThread.Run(async () =>
    {
        _ = await Connected();

        Assert.Contains(new Ui.Api.Workflow.InstanceQuery(Root: true, Open: true), _server.InstanceQueries);
        // The board's Done lane reads the finished roots (open=false); every other read is the open one.
        Assert.All(_server.InstanceQueries, q => Assert.True(q is { Root: true } && (q.Open is true || q == new Ui.Api.Workflow.InstanceQuery(Root: true, Open: false))));
    }, Token);

    [Fact]
    public Task A_started_incident_moves_to_the_top_without_a_click() => UiThread.Run(async () =>
    {
        _server.WorkflowAnswer = WithoutTheFirstRow();
        var shell = await Connected();
        var incident = FixtureInstances.Rows[0].Address;
        Assert.DoesNotContain(shell.Strip.Rows, r => string.Equals(r.Address, incident, StringComparison.Ordinal));

        _server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, FixtureBody());
        _server.Push(A.Event(71, "workflow.started"));

        await ShellWait.Until(shell, () => shell.Strip.Rows.Count > 0 && string.Equals(shell.Strip.Rows[0].Address, incident, StringComparison.Ordinal), Token);
    }, Token);

    [Fact]
    public Task Strip_refresh_never_moves_focus_scroll_or_input() => UiThread.Run(async () =>
    {
        _server.WorkflowAnswer = WithoutTheFirstRow();
        var shell = await Connected();
        var held = shell.Strip.Rows[2].Address;
        Assert.True(shell.Strip.Select(held));
        var calls = _server.InstancesCalls;

        _server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, FixtureBody());
        _server.Push(A.Event(72, "workflow.started"));
        await ShellWait.Until(shell, () => _server.InstancesCalls > calls && shell.Strip.Rows.Count > 0 && string.Equals(shell.Strip.Rows[0].Address, FixtureInstances.Rows[0].Address, StringComparison.Ordinal), Token);

        Assert.Equal(held, shell.Strip.SelectedAddress);
        _ = Assert.Single(shell.Strip.Rows, r => r.IsSelected);
        Assert.Null(shell.NeedsYou.SelectedId);
        Assert.Null(shell.Queue.SelectedId);
        Assert.False(shell.Queue.IsGuarded);
        Assert.Equal(PageId.Home, shell.CurrentPage);
    }, Token);

    [Fact]
    public Task A_missing_list_route_marks_only_the_strip() => UiThread.Run(async () =>
    {
        var shell = await Connected();

        _server.WorkflowAnswer = RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-API-404", "no such route");
        _server.Push(A.Event(73, "workflow.started"));
        await ShellWait.Until(shell, () => shell.Strip.IsNotServed, Token);

        Assert.Equal((ShellState.Normal, PageId.Home), (shell.State, shell.CurrentPage));
    }, Token);

    [Fact]
    public Task Another_failure_keeps_the_rows_as_they_were() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var shown = shell.Strip.Rows.Select(r => r.Address).ToList();
        var (calls, reads) = (_server.InstancesCalls, shell.Health.ViewReads);

        _server.WorkflowAnswer = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-099", "busy");
        _server.Push(A.Event(73, "workflow.started"));
        await ShellWait.Until(shell, () => shell.Health.ViewReads > reads, Token);   // the strip is the last view read: its failed answer was handled
        Assert.True(_server.InstancesCalls > calls);

        Expect.Sequence(shown, shell.Strip.Rows.Select(r => r.Address));
        Assert.False(shell.Strip.IsNotServed);
    }, Token);

    // review run 1, corr-13: the "can't be read" sentence is for a body this build can't read, not for a server error with no problem body.
    [Fact]
    public Task A_plain_500_keeps_the_rows_and_does_not_say_the_list_cant_be_read() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var shown = shell.Strip.Rows.Select(r => r.Address).ToList();
        var reads = shell.Health.ViewReads;

        _server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.InternalServerError, "oops");
        _server.Push(A.Event(74, "workflow.started"));
        await ShellWait.Until(shell, () => shell.Health.ViewReads > reads, Token);

        Expect.Sequence(shown, shell.Strip.Rows.Select(r => r.Address));
        Assert.False(shell.Strip.IsUnreadable);
    }, Token);

    // Proof B6: main's list still serves P4's shape (no counts): the strip says so in a sentence, never an empty box.
    [Fact]
    public Task A_list_in_a_shape_this_build_cant_read_shows_the_unreadable_sentence_and_no_rows() => UiThread.Run(async () =>
    {
        var shell = await Connected();

        _server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, """{"instances":[],"needsYou":[],"empty":"nothing","rows":[]}""");
        _server.Push(A.Event(73, "workflow.started"));
        await ShellWait.Until(shell, () => shell.Strip.IsUnreadable, Token);

        Assert.Equal((Labels.StripUnreadable, 0, false), (shell.Strip.UnreadableText, shell.Strip.Rows.Count, shell.Strip.IsNotServed));
        Assert.Equal(ShellState.Normal, shell.State);
    }, Token);

    [Fact]
    public Task The_sentence_goes_when_the_list_can_be_read_again() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        _server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, """{"instances":[],"needsYou":[],"empty":"nothing","rows":[]}""");
        _server.Push(A.Event(73, "workflow.started"));
        await ShellWait.Until(shell, () => shell.Strip.IsUnreadable, Token);

        _server.WorkflowAnswer = null;
        _clock.Advance(HealthViewModel.RefreshEvery);   // the next re-read is a quarter second away (§9.2)
        _server.Push(A.Event(74, "workflow.started"));
        await ShellWait.Until(shell, () => !shell.Strip.IsUnreadable, Token);

        Assert.True(shell.Strip.Rows.Count > 0);
    }, Token);
}
