using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>#2823: an open Workflows page reads its list again when a workflow is created, revised, activated, paused or archived, the same way it does after a run event. The stream carries workflow.created|revised|activated|paused|archived; the other workflow.* types (notify, taken-over) read nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellWorkflowDefinitionEventsTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> OpenOnWorkflows()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await Until(() => shell.State == ShellState.Normal);
        shell.GoTo(PageId.Workflows);
        await Until(() => shell.Workflows.Rows.Count == 2);
        return shell;
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 400 && !condition(); i++)
        {
            await Task.Delay(5, Token);
        }

        Assert.True(condition());
    }

    // One event per test: the follower re-reads at most once per 250 ms and the fake clock never moves, so a second event would wait forever.
    [Theory]
    [InlineData("workflow.created")]
    [InlineData("workflow.revised")]
    [InlineData("workflow.activated")]
    [InlineData("workflow.paused")]
    [InlineData("workflow.archived")]
    public Task A_workflow_definition_event_reads_the_list_again(string type) => UiThread.Run(async () =>
    {
        var shell = await OpenOnWorkflows();
        var lists = _server.WorkflowsCalls;

        _server.Push(A.Event(80, type));

        await Until(() => _server.WorkflowsCalls > lists);
    }, Token);

    [Fact]
    public Task An_activated_event_flips_the_row_to_on_in_one_read() => UiThread.Run(async () =>
    {
        var shell = await OpenOnWorkflows();
        var off = shell.Workflows.Rows.First(r => !r.IsActive);
        var list = WorkflowsFixture.Golden("workflows", "list-two");
        var index = Array.FindIndex([.. shell.Workflows.Rows], r => string.Equals(r.Slug, off.Slug, StringComparison.Ordinal));
        list["workflows"]![index]!["activeRevision"] = 1;
        _server.WorkflowsAnswer = new RouteAnswer(HttpStatusCode.OK, list.ToJsonString());

        _server.Push(A.Event(81, "workflow.activated"));

        await Until(() => shell.Workflows.Rows.Single(r => string.Equals(r.Slug, off.Slug, StringComparison.Ordinal)).IsActive);
    }, Token);

    [Theory]
    [InlineData("workflow.notify")]
    [InlineData("workflow.taken-over")]
    public Task Other_workflow_events_read_nothing(string type) => UiThread.Run(async () =>
    {
        var shell = await OpenOnWorkflows();
        var (lists, reads) = (_server.WorkflowsCalls, shell.Health.ViewReads);

        _server.Push(A.Event(82, type));
        await Until(() => shell.Health.ViewReads > reads);

        Assert.Equal(lists, _server.WorkflowsCalls);
    }, Token);
}
