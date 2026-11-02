using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Setup;

/// <summary>Home's Needs-you header and card agree on the setup rows whether or not the first setup read has answered (A-6, #1060).</summary>
[Lane(TestLane.Unit)]
public sealed class ShellSetupCountTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.SpecShaped("commands", "extra-approve-all-ready") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // The fixture's three rows (a decision, a failure, a profile row) and the two setup rows.
    private static RouteAnswer ThreeAndTwoSetup()
    {
        var list = GoldenFiles.Node("needs-you", "fixture");
        var rows = list["rows"]!.AsArray();
        foreach (var row in JsonNode.Parse(GoldenFiles.SpecShaped("needs-you", "extra-setup-rows"))!["rows"]!.AsArray().ToArray())
        {
            rows.Add(JsonNode.Parse(row!.ToJsonString()));
        }

        return new RouteAnswer(System.Net.HttpStatusCode.OK, list.ToJsonString());
    }

    private async Task<ShellViewModel> Connected(Func<ShellViewModel, bool> ready)
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal && ready(shell), Token);
        return shell;
    }

    [Fact]
    public Task While_the_first_setup_read_is_out_the_header_and_the_card_both_count_every_row() => UiThread.Run(async () =>
    {
        _server.NeedsYouView = ThreeAndTwoSetup();
        _server.SetupGate = new TaskCompletionSource();
        var shell = await Connected(s => s.NeedsYou.Count > 0 && s.Setup.IsReading);

        Assert.Equal((5, 5), (shell.NeedsYou.Count, shell.NeedsYou.Rows.Count));   // a read that has not answered holds nothing back: the header can't jump when it does
        Assert.StartsWith("5 need you", shell.Attention.SummaryLine, StringComparison.Ordinal);
        _server.SetupGate.SetResult();
    }, Token);

    [Fact]
    public Task When_the_first_read_leaves_a_step_open_only_the_row_of_the_step_the_line_names_stays_out() => UiThread.Run(async () =>
    {
        _server.NeedsYouView = ThreeAndTwoSetup();
        _server.SetupAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, SetupFixture.With("extra-priority-mix", "profile", "done").ToJsonString());   // the first open step is recipes
        var shell = await Connected(s => s.Setup.IsHomeLineShown);

        Assert.Equal("recipes", shell.Setup.HomeStepId);
        Assert.Equal((4, 4), (shell.NeedsYou.Count, shell.NeedsYou.Rows.Count));   // setup:recipes is the line's; setup:mcp stays
        Assert.StartsWith("4 need you", shell.Attention.SummaryLine, StringComparison.Ordinal);
    }, Token);

    [Fact]
    public Task When_the_first_read_finds_setup_finished_the_setup_rows_come_back_in_both() => UiThread.Run(async () =>
    {
        _server.NeedsYouView = ThreeAndTwoSetup();
        _server.SetupAnswer = RouteAnswer.Api("setup", "all-done");
        var shell = await Connected(s => s.Setup.IsFinished);

        Assert.Equal((5, 5), (shell.NeedsYou.Count, shell.NeedsYou.Rows.Count));
        Assert.StartsWith("5 need you", shell.Attention.SummaryLine, StringComparison.Ordinal);
    }, Token);
}
