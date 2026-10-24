using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>One click to where the owner acts (q65): a nested park's row opens the child's workflow frame at once, from Home, the inbox and the tray; every other kind keeps its sheet.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellOpenTargetTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Connected(string scenario, string? via = null)
    {
        var park = JsonNode.Parse(GoldenFiles.Api("needs-you", scenario))!;
        if (via is not null)
        {
            park["rows"]![0]!["via"] = via;
        }

        _server.NeedsYouView = new RouteAnswer(HttpStatusCode.OK, park.ToJsonString());
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal && shell.NeedsYou.Seen.Rows.Count > 0, Token);
        return shell;
    }

    private static string Child() => (string)GoldenFiles.Node("needs-you", "park-mirrored-child")["rows"]![0]!["child"]!;

    [Fact]
    public Task A_nested_parks_row_has_the_child_as_its_open_link() => UiThread.Run(async () =>
    {
        var shell = await Connected("park-mirrored-child");

        var row = shell.NeedsYou.Seen.Rows[0];

        Assert.Equal((new DeepLink(DeepLinkKind.Workflow, Child()), true), (row.OpenLink, row.OpensChild));
    }, Token);

    [Fact]
    public Task Review_and_decide_on_a_run_whose_child_is_parked_ends_at_the_answer_form() => UiThread.Run(async () =>
    {
        var shell = await Connected("park-mirrored-child");
        var instance = JsonNode.Parse(GoldenFiles.Api("workflow-instance", "project-build"))!;
        instance["itemId"] = null;
        var parked = instance["children"]![0]!["workstreams"]![1]!["rows"]![0]!;
        parked["address"] = Child();   // the child the mirrored park row points at
        parked["chip"] = JsonNode.Parse("""{"word":"PARKED","tone":"neutral"}""");
        _server.InstanceBody = _ => new RouteAnswer(HttpStatusCode.OK, instance.ToJsonString());
        var detail = FixtureInstances.Detail("project-build");
        var summary = FixtureInstances.List("open").Instances.First(r => string.Equals(r.Id, detail.Id, StringComparison.Ordinal)) with { Chip = new Ui.Api.Workflow.StateChip("PARKED", Ui.Api.Workflow.ChipTone.Neutral) };
        var inspector = shell.Workflows.Inspector;
        await inspector.Show(new Uri("http://127.0.0.1:7410/"), new ViewModels.Workflows.InstanceRowViewModel(summary, new FakeClipboard(), _clock), Token);

        await inspector.ReviewAndDecide(Token);

        Assert.True(shell.IsNeedsYouInbox);
        Assert.True(shell.Inbox.ShowsAnswer);   // the answer form, not the child's copy-only frame

        var form = shell.Inbox.Answer!;   // typing there sends the answer-text for the parked step the row names
        form.Choose(ViewModels.NeedsYou.PausedAnswerOption.Hint);
        form.HintText = " try the other recipe ";
        await form.Send();

        var (target, body) = Assert.Single(_server.CommandCalls);
        Assert.Equal((string)GoldenFiles.Node("needs-you", "park-mirrored-child")["rows"]![0]!["subject"]!["id"]!, target);
        Assert.Equal((Ui.Api.Workflow.CommandKind.Answer, (Ui.Api.Workflow.AnswerVariant?)Ui.Api.Workflow.AnswerVariant.Text, (string?)"try the other recipe"), (body.Kind, body.Answer, body.Text));
        Assert.True(form.IsSent);
    }, Token);

    [Fact]
    public Task A_plain_park_keeps_its_sheet_as_the_open_link() => UiThread.Run(async () =>
    {
        var shell = await Connected("park");

        var row = shell.NeedsYou.Seen.Rows[0];

        Assert.Equal((new DeepLink(DeepLinkKind.NeedsYou, row.Id), false), (row.OpenLink, row.OpensChild));
    }, Token);

    [Fact]
    public Task A_child_pointer_served_no_child_keeps_the_sheet_where_the_pointer_is() => UiThread.Run(async () =>
    {
        var shell = await Connected("park", via: "child");

        var row = shell.NeedsYou.Seen.Rows[0];

        Assert.Equal((row.SheetLink, false), (row.OpenLink, row.OpensChild));
    }, Token);

    [Fact]
    public Task Clicking_a_nested_parks_row_opens_the_childs_frame_in_one_step() => UiThread.Run(async () =>
    {
        var shell = await Connected("park-mirrored-child");

        shell.OpenNeedsYouRow(shell.NeedsYou.Seen.Rows[0].Id);

        Assert.Equal(new DeepLink(DeepLinkKind.Workflow, Child()), shell.SheetLink);
    }, Token);

    [Fact]
    public Task Clicking_any_other_row_opens_the_inbox_on_that_row() => UiThread.Run(async () =>
    {
        var shell = await Connected("park");

        shell.OpenNeedsYouRow(shell.NeedsYou.Seen.Rows[0].Id);

        Assert.True(shell.IsNeedsYouInbox);
    }, Token);

    [Fact]
    public Task The_tray_row_takes_a_nested_park_to_its_child_not_to_the_inbox() => UiThread.Run(async () =>
    {
        var shell = await Connected("park-mirrored-child");
        var rowId = shell.NeedsYou.Seen.Rows[0].Id;

        shell.Navigate(new DeepLink(DeepLinkKind.NeedsYou, rowId));   // what the tray's row navigates to

        Assert.Equal(new DeepLink(DeepLinkKind.Workflow, Child()), shell.SheetLink);
        Assert.False(shell.IsNeedsYouInbox);
    }, Token);

    [Fact]
    public Task The_tray_row_of_any_other_kind_still_opens_the_inbox_on_that_row() => UiThread.Run(async () =>
    {
        var shell = await Connected("park");
        var rowId = shell.NeedsYou.Seen.Rows[0].Id;

        shell.Navigate(new DeepLink(DeepLinkKind.NeedsYou, rowId));

        Assert.True(shell.IsNeedsYouInbox);
    }, Token);
}
