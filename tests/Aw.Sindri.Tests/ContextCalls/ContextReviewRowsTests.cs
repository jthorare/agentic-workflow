using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Testing;
using Table = Aw.Sindri.Core.Commands.Authorization;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>
/// The classes of the S-4 context commands (README X63; context spec §8.5, §8.7): single decisions, the bulk deterministic accept,
/// undo and the orphan repoint and drop are desk (a reversible override row with Undo); applying a review file, merge, add, delete
/// and promote are protected; proposing is a job callable from the terminal, the app and the engine's services; building and syncing sources are jobs on the P6 runtime.
/// </summary>
public sealed class ContextReviewRowsTests
{
    private static readonly CommandCatalog _catalog = new();

    private static readonly string[] _desk = ["accept", "reject", "rename", "move", "accept-deterministic", "undo", "drop", "repoint"];
    private static readonly string[] _protected = ["apply-file", "merge", "add", "delete"];

    private static CommandRow Row(string action) => Assert.Single(_catalog.Rows, r => r.Action.Value == action);

    [Fact, Trait("Lane", "unit")]
    public void Every_desk_review_variant_is_desk_and_ready_and_every_protected_one_needs_a_terminal_and_the_typed_confirmation()
    {
        Assert.All(_desk, v =>
        {
            var row = Row("ReviewContextMap:" + v);
            Assert.Equal((ActionClass.Desk, false, "ready", "POST", "/sindri/context/map/review"), (row.Class, row.NeedsTerminal, row.Ui, row.Method, row.Route));
            Assert.Equal(typeof(ReviewContextMapRequest), row.CommandType);
        });
        Assert.All(_protected, v =>
        {
            var row = Row("ReviewContextMap:" + v);
            Assert.Equal((ActionClass.Protected, true, "cli-only:typed-confirmation"), (row.Class, row.NeedsTerminal, row.Ui));
            Assert.Equal(typeof(ReviewContextMapRequest), row.CommandType);
        });
    }

    [Fact, Trait("Lane", "unit")]
    public void The_service_asks_for_a_typed_answer_exactly_where_the_catalog_says_protected_and_takes_exactly_the_catalog_variants()
    {
        var reviewRows = _catalog.Rows.Where(r => r.Action.Value.StartsWith("ReviewContextMap:", StringComparison.Ordinal)).ToList();
        static string Verb(CommandRow r) => r.Action.Value["ReviewContextMap:".Length..];

        Assert.Equal(reviewRows.Where(r => r.Class == ActionClass.Protected).Select(Verb).Order(StringComparer.Ordinal), ReviewInput.Protected.Order(StringComparer.Ordinal));
        Assert.Equal(reviewRows.Select(Verb).Order(StringComparer.Ordinal), ReviewInput.Verbs.Order(StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void Promote_is_protected_and_proposing_is_a_job_the_app_the_terminal_and_the_engine_may_call_but_a_session_may_not()
    {
        var promote = Row("PromoteContextMap");
        var propose = Row("ProposeContextMap");

        Assert.Equal((ActionClass.Protected, true, "cli-only:typed-confirmation", "/sindri/context/map/promote"), (promote.Class, promote.NeedsTerminal, promote.Ui, promote.Route));
        Assert.Equal((ActionClass.Job, "ready", "POST", "/sindri/context/map/proposals"), (propose.Class, propose.Ui, propose.Method, propose.Route));
        Assert.True(Table.Decide(propose.Class, A.Caller().AtTerminal().Build(), propose).Allowed);
        Assert.True(Table.Decide(propose.Class, A.Caller().Human().Build(), propose).Allowed);
        Assert.True(Table.Decide(propose.Class, A.Caller().Service("engine").Build(), propose).Allowed);
        Assert.False(Table.Decide(propose.Class, A.Caller().Session().Build(), propose).Allowed);
    }

    [Fact, Trait("Lane", "unit")]
    public void Map_build_is_a_ready_job_row_for_the_build_request()
    {
        var build = Row("BuildContextMap");

        Assert.Equal((ActionClass.Job, "ready", "POST", "/sindri/context/map/build"), (build.Class, build.Ui, build.Method, build.Route));
        Assert.Equal(typeof(BuildContextMapRequest), build.CommandType);
        Assert.Equal("sindri context map build", build.Cli.Text);
    }

    [Fact, Trait("Lane", "unit")]
    public void Run_context_backtest_is_a_ready_job_row_for_the_backtest_request()
    {
        var backtest = Row("RunContextBacktest");

        Assert.Equal((ActionClass.Job, "ready", "POST", "/sindri/context/eval/backtest"), (backtest.Class, backtest.Ui, backtest.Method, backtest.Route));
        Assert.Equal(typeof(RunContextBacktestRequest), backtest.CommandType);
        Assert.Equal("sindri context eval backtest", backtest.Cli.Text);
        Assert.False(Table.Decide(backtest.Class, A.Caller().Session().Build(), backtest).Allowed);
    }

    [Fact, Trait("Lane", "unit")]
    public void Sync_sources_is_a_ready_job_row_for_the_sync_request()
    {
        var sync = Row("SyncSources");

        Assert.Equal((ActionClass.Job, "ready", "POST", "/sindri/context/sources/sync"), (sync.Class, sync.Ui, sync.Method, sync.Route));
        Assert.Equal(typeof(SyncContextSourcesRequest), sync.CommandType);
        Assert.Equal("sindri context sources sync", sync.Cli.Text);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_two_context_jobs_are_refused_to_a_session_and_allowed_to_the_terminal_the_app_and_a_service()
    {
        foreach (var row in new[] { Row("BuildContextMap"), Row("SyncSources") })
        {
            Assert.True(Table.Decide(row.Class, A.Caller().AtTerminal().Build(), row).Allowed);
            Assert.True(Table.Decide(row.Class, A.Caller().Human().Build(), row).Allowed);
            Assert.True(Table.Decide(row.Class, A.Caller().Service("engine").Build(), row).Allowed);
            Assert.False(Table.Decide(row.Class, A.Caller().Session().Build(), row).Allowed);
        }
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("accept")]
    [InlineData("reject")]
    [InlineData("rename")]
    [InlineData("move")]
    [InlineData("accept-deterministic")]
    [InlineData("undo")]
    [InlineData("drop")]
    [InlineData("repoint")]
    public void A_desk_decision_is_allowed_for_the_terminal_and_the_app_and_refused_for_a_session(string verb)
    {
        var request = new ReviewContextMapRequest(verb);
        var row = ActionClassifier.Classify(_catalog, request);

        Assert.True(Table.Decide(row.Class, A.Caller().AtTerminal().Build(), row).Allowed);
        Assert.True(Table.Decide(row.Class, A.Caller().Human().Build(), row).Allowed);
        var session = Table.Decide(row.Class, A.Caller().Session().Build(), row);
        Assert.False(session.Allowed);
        Assert.Equal(SindriErrors.AuthRefused.Code, session.Refusal?.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("apply-file")]
    [InlineData("merge")]
    [InlineData("add")]
    [InlineData("delete")]
    public void A_protected_review_action_is_allowed_for_the_terminal_only(string verb)
    {
        var row = ActionClassifier.Classify(_catalog, new ReviewContextMapRequest(verb));

        Assert.True(Table.Decide(row.Class, A.Caller().AtTerminal().Build(), row).Allowed);
        Assert.False(Table.Decide(row.Class, A.Caller().Human().Build(), row).Allowed);
        Assert.False(Table.Decide(row.Class, A.Caller().Session().Build(), row).Allowed);
        Assert.False(Table.Decide(row.Class, A.Caller().Service("engine").Build(), row).Allowed);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_review_verb_fails_closed_to_a_protected_terminal_only_row()
    {
        var row = ActionClassifier.Classify(_catalog, new ReviewContextMapRequest("obliterate"));

        Assert.Equal(ActionClass.Protected, row.Class);
        Assert.True(row.NeedsTerminal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_review_input_reads_each_verb_and_names_a_bad_field()
    {
        var accept = ReviewInput.Read(new ReviewContextMapRequest("accept", Node: "feature:tabs"));
        var link = ReviewInput.Read(new ReviewContextMapRequest("reject", Link: "edge:0123456789abcdef"));
        var badNode = ReviewInput.Read(new ReviewContextMapRequest("accept", Node: "nonsense"));
        var noFile = ReviewInput.Read(new ReviewContextMapRequest("apply-file"));
        var unknown = ReviewInput.Read(new ReviewContextMapRequest("obliterate"));

        Assert.Equal(("accept", false), (accept.Value.Verb, accept.Value.IsProtected));
        Assert.Equal("edge:0123456789abcdef", link.Value.Decision!.Link!.Value.Value);
        Assert.Contains("node:", badNode.Error.Message, StringComparison.Ordinal);
        Assert.Equal(SindriErrors.InvalidInput.Code, noFile.Error.Code);
        Assert.Equal(SindriErrors.InvalidInput.Code, unknown.Error.Code);
        Assert.True(ReviewInput.Read(new ReviewContextMapRequest("apply-file", Path: "/x/review.json")).Value.IsProtected);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_review_input_reads_an_undo_list_as_all_or_none_ids_and_refuses_it_beside_a_single_override()
    {
        var list = ReviewInput.Read(new ReviewContextMapRequest("undo", Overrides: ["ovr_0123456789abcdef", "ovr_fedcba9876543210"]));
        var both = ReviewInput.Read(new ReviewContextMapRequest("undo", Override: "ovr_0123456789abcdef", Overrides: ["ovr_fedcba9876543210"]));
        var empty = ReviewInput.Read(new ReviewContextMapRequest("undo", Overrides: []));
        var tooMany = ReviewInput.Read(new ReviewContextMapRequest("undo", Overrides: [.. Enumerable.Repeat("ovr_0123456789abcdef", 10_001)]));
        var tenThousand = ReviewInput.Read(new ReviewContextMapRequest("undo", Overrides: [.. Enumerable.Repeat("ovr_0123456789abcdef", 10_000)]));
        var bad = ReviewInput.Read(new ReviewContextMapRequest("undo", Overrides: ["ovr_0123456789abcdef", "nonsense"]));
        var wrongVerb = ReviewInput.Read(new ReviewContextMapRequest("drop", Overrides: ["ovr_0123456789abcdef"]));

        Assert.Equal(["ovr_0123456789abcdef", "ovr_fedcba9876543210"], list.Value.Decision!.UndoesAll!.Select(i => i.Value), StringComparer.Ordinal);
        Assert.Null(list.Value.Decision.Undoes);
        Assert.Equal(10_000, tenThousand.Value.Decision!.UndoesAll!.Count);
        Assert.All([both, empty, tooMany, bad, wrongVerb], r => Assert.Equal(SindriErrors.InvalidInput.Code, r.Error.Code));
        Assert.Contains("overrides", both.Error.Message, StringComparison.Ordinal);
        Assert.Contains("overrides[1]", bad.Error.Message, StringComparison.Ordinal);
    }
}
