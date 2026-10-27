using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

// The #1057 check with the instance served: the original's subject has no instance golden, so the bar never gets a desk run to send against.
/// <summary>Home's strip Answer on a desk-task step (#1057): it opens the Needs-you inbox on that row, where the answer bar sends the step's CompleteStep, not the read-only row sheet; and Cancel, which the app sends, carries no "not available" chip.</summary>
[Lane(TestLane.Unit)]
public sealed class StripAnswerDeskServedTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static bool IsDeskAnswer(InstanceSummary row) =>
        row.Answer is null && row.Actions.Contains("CompleteStep", StringComparer.Ordinal) && row.States.Any(s => s.Desk && string.Equals(s.State, row.State, StringComparison.Ordinal));

    private async Task<(ShellViewModel Shell, string Address, string DeskId)> Connected()
    {
        var desk = JsonNode.Parse(GoldenFiles.Api("needs-you", "desk-task"))!;
        var deskId = desk["rows"]![0]!["id"]!.GetValue<string>();
        var instanceId = desk["rows"]![0]!["subject"]!["id"]!.GetValue<string>();
        _server.NeedsYouView = new RouteAnswer(HttpStatusCode.OK, desk.ToJsonString());
        var served = FixtureInstances.Rows.First(IsDeskAnswer).Id;
        var list = GoldenFiles.Node("workflow-instances", "root");
        var kept = list["instances"]!.AsArray().First(r => string.Equals(r!["id"]!.GetValue<string>(), served, StringComparison.Ordinal))!;
        var row = JsonNode.Parse(kept.ToJsonString())!;
        row["id"] = instanceId;
        list["instances"] = new JsonArray(row);
        _server.InstancesBody = new RouteAnswer(HttpStatusCode.OK, list.ToJsonString());
        _server.InstanceBody = _ => new RouteAnswer(HttpStatusCode.OK, GoldenFiles.Api("workflow-instance", "dev-desk-round-trip"));
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal && shell.Strip.Rows.Count > 0 && shell.NeedsYou.Seen.Rows.Count > 0, Token);
        return (shell, shell.Strip.Rows[0].Address, deskId);
    }

    [Fact]
    public Task Answer_on_a_desk_step_opens_the_inbox_on_its_row_with_the_answer_bar_and_sends_one_CompleteStep() => UiThread.Run(async () =>
    {
        var (shell, address, deskId) = await Connected();

        shell.AnswerStripRow(address);

        Assert.True(shell.IsNeedsYouInbox, "Answer opened the read-only row sheet, not the Needs-you inbox");
        Assert.Equal(deskId, shell.Inbox.Selected?.Id);
        await shell.Inbox.Bar.Loaded;
        Assert.True(shell.Inbox.Bar.HasBar, "the inbox row has no answer bar");

        shell.Inbox.Bar.ReadToEnd = true;
        await shell.Inbox.Bar.PressApprove();

        _ = Assert.Single(_server.CompleteCalls);
    }, Token);

    [Fact]
    public Task Cancel_in_the_instance_frame_has_no_not_available_chip_because_the_app_sends_it() => UiThread.Run(async () =>
    {
        var cancel = new StatusCommand(StatusVerb.Cancel, "Cancel", "normal", "sindri workflow cancel ITM-1/x/1", Confirm: null);

        var command = new ExpandedCommandViewModel(cancel, answer: null, "ITM-1/x/1", new FakeClipboard(), _clock);

        Assert.True(command.PendingChip.Length == 0, $"Cancel still carries the chip \"{command.PendingChip}\"");
        await Task.CompletedTask;
    }, Token);
}
