using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Item;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Item;

/// <summary>The Decide line is copyable (review run 4, a11y; UI spec section 7): a Copy named for the decision puts the served command on the clipboard, and only when there is one.</summary>
[Lane(TestLane.Unit)]
public sealed class DecideLineCopyTests
{
    private const string DecisionItem = "ITM-0000000000000000000000000000000e";
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ItemSheetViewModel> Sheet(string decideUi)
    {
        _server.DecisionsByItem[DecisionItem] = RouteAnswer.Api("decisions", "by-item");
        _server.RequestsByItem[DecisionItem] = RouteAnswer.Api("requests", "list-by-item");
        _server.Items[DecisionItem] = _server.Items[A.NewItem];
        var listed = (await _server.Commands(_api, Token)).Value;
        var catalog = new CatalogViewModel(_clipboard, _clock);
        catalog.Show(_api, _server, new CommandList([.. listed.Rows.Select(r => string.Equals(r.Action, "Decide", StringComparison.Ordinal) ? r with { Ui = decideUi } : r)]));
        var sheet = new ItemSheetViewModel(_server, catalog, _clock);
        await sheet.Open(_api, DecisionItem, Token);
        return sheet;
    }

    private DecisionSheetViewModel DecisionSheet(ActionReadiness readiness)
    {
        var served = NeedsYouRows.Decision();
        var row = served with { Actions = [.. served.Actions.Select(a => a with { Readiness = readiness, Cli = "sindri decide DEC-1 " + a.OptionId })] };
        var sheet = new DecisionSheetViewModel(new FakeSindriApi(), () => null, _clock, new DecisionSender(new FakeSindriApi()), _clipboard);
        sheet.Show(new NeedsYouRowViewModel(row, seen: true, reducedMotion: false, arrived: false, _clock));
        return sheet;
    }

    [Fact]
    public Task A_ready_Decide_has_no_line_and_nothing_to_copy() => UiThread.Run(async () =>
    {
        var sheet = await Sheet("ready");

        Assert.False(sheet.Needs.Decisions[0].CanCopy);
        await sheet.Needs.CopyDecide(sheet.Needs.Decisions[0].Id);
        Assert.Null(_clipboard.Text);
    }, Token);

    [Fact]
    public async Task The_decision_sheets_line_copies_the_served_command()
    {
        var sheet = DecisionSheet(ActionReadiness.Pending);

        Assert.Equal("sindri decide DEC-1 accept", sheet.DecideCli);
        await sheet.CopyDecide();

        Assert.Equal("sindri decide DEC-1 accept", _clipboard.Text);
        Assert.StartsWith(sheet.DecisionId, sheet.DecideLineName, StringComparison.Ordinal);
        Assert.Equal("Copy: Decide " + sheet.DecisionId, sheet.CopyName);
    }
}
