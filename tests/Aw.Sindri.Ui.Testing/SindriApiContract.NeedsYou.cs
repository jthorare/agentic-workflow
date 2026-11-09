using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The contract (<see cref="SindriApiContract"/>, continued): the P4 routes answer as the others do, fake and client alike.</summary>
public abstract partial class SindriApiContract
{
    [Fact]
    public async Task NeedsYou_without_work_returns_the_served_setup_row_and_no_sentence()
    {
        var server = new FakeSindriApi { NeedsYouView = RouteAnswer.Api("needs-you", "empty") };

        var list = (await Reach(server).NeedsYou(_api, Token)).Value;

        Expect.Sequence(["setup:mcp"], list.Rows.Select(r => r.Id));
        Expect.Equal(("RegisterMcp", null), (list.Rows[0].PrimaryAction, list.Empty));
        Expect.Equal((string?)GoldenFiles.Node("needs-you", "empty")["empty"], list.Empty);
    }

    [Fact]
    public async Task NeedsYou_stalled_keeps_rows_and_flags_stalled()
    {
        var server = new FakeSindriApi { NeedsYouView = RouteAnswer.Api("needs-you", "stalled") };

        var list = (await Reach(server).NeedsYou(_api, Token)).Value;

        var served = GoldenFiles.Node("needs-you", "stalled");
        Expect.Equal(served["rows"]!.AsArray().Count, list.Rows.Count);
        Expect.Equal((true, (long)served["asOf"]!["cursor"]!, (long)served["asOf"]!["ledgerHead"]!), (list.AsOf.Stalled, list.AsOf.Cursor, list.AsOf.LedgerHead));
    }

    [Fact]
    public async Task Decide_conflict_is_a_problem_not_an_exception()
    {
        var server = new FakeSindriApi { Decided = RouteAnswer.Problem("decisions-id", "decided-elsewhere") };

        var result = await Reach(server).Decide(_api, "DEC-0192a1b2c3d4e5f6a7b8c9d0e1f2d701", new DecideRequest("accept", Note: null), Token);

        Expect.True(result.IsError);
        Expect.Equal(((string)GoldenFiles.Node("decisions-id", "decided-elsewhere")["title"]!, ErrorKind.Conflict), (result.Error.Code, result.Error.Kind));
    }

    // review run 1, arch-11 / test-5: the rest of API P4's wire, each against its golden, fake and client alike.
    private static string[] Ids(string slug, string scenario, string member) => [.. GoldenFiles.Node(slug, scenario)[member]!.AsArray().Select(n => (string)n!["id"]!)];

    [Fact]
    public async Task Decisions_pending_and_decided_return_their_own_goldens()
    {
        var server = new FakeSindriApi();

        var pending = (await Reach(server).Decisions(_api, pending: true, item: null, Token)).Value;
        var decided = (await Reach(server).Decisions(_api, pending: false, item: null, Token)).Value;

        Expect.Sequence(Ids("decisions", "pending", "decisions"), pending.Decisions.Select(d => d.Id));
        Expect.Sequence(Ids("decisions", "decided", "decisions"), decided.Decisions.Select(d => d.Id));
        Expect.Sequence([true, false], server.DecisionQueries.Select(q => q.Pending == true));
        Expect.Equal(0, server.DecisionQueries.Count(q => q.Item is not null));
    }

    [Fact]
    public async Task Decisions_for_an_item_return_that_items_golden()
    {
        const string item = "ITM-0000000000000000000000000000000e";
        var server = new FakeSindriApi();
        server.DecisionsByItem[item] = RouteAnswer.Api("decisions", "by-item");

        var list = (await Reach(server).Decisions(_api, pending: true, item, Token)).Value;

        Expect.Sequence(Ids("decisions", "by-item", "decisions"), list.Decisions.Select(d => d.Id));
        Expect.Equal((true, item), (server.DecisionQueries[^1].Pending, server.DecisionQueries[^1].Item));
    }

    [Fact]
    public async Task Requests_for_an_item_return_that_items_golden()
    {
        const string item = "ITM-0000000000000000000000000000000e";
        var server = new FakeSindriApi();
        server.RequestsByItem[item] = RouteAnswer.Api("requests", "list-by-item");

        var list = (await Reach(server).Requests(_api, state: null, item, Token)).Value;

        Expect.Sequence(Ids("requests", "list-by-item", "requests"), list.Requests.Select(r => r.Id));
        Expect.Equal(item, server.RequestQueries[^1].Item);
    }

    [Fact]
    public async Task Decide_accepted_returns_the_accepted_sequence_and_sends_the_choice_and_note()
    {
        var server = new FakeSindriApi();

        var accepted = (await Reach(server).Decide(_api, "DEC-0192a1b2c3d4e5f6a7b8c9d0e1f2d701", new DecideRequest("accept", "go"), Token)).Value;

        Expect.Equal((long)GoldenFiles.Node("decisions-id", "accepted")["sequence"]!, accepted.Sequence);
        Expect.Equal(1, server.DecideCalls.Count);
        var (decision, body) = server.DecideCalls[0];
        Expect.Equal(("DEC-0192a1b2c3d4e5f6a7b8c9d0e1f2d701", "accept", "go"), (decision, body.Choice, body.Note));
    }

    [Fact]
    public async Task FileRequest_returns_the_filed_ids_and_sends_kind_item_and_text()
    {
        var server = new FakeSindriApi();

        var filed = (await Reach(server).FileRequest(_api, new FileRequestRequest("rescope", "ITM-1", "Split it"), Token)).Value;

        var served = GoldenFiles.Node("requests", "filed");
        Expect.Equal(((string)served["requestId"]!, (string)served["decisionId"]!), (filed.RequestId, filed.DecisionId));
        Expect.Equal(1, server.FileCalls.Count);
        Expect.Equal(new FileRequestRequest("rescope", "ITM-1", "Split it"), server.FileCalls[0]);
    }
}
