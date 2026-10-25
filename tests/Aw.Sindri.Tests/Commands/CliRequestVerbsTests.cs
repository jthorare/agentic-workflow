using System.Net;
using System.Text;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Decisions;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.FileRequest;
using Aw.Sindri.Features.GetNeedsYou;
using Aw.Sindri.Features.ListDecisions;
using Aw.Sindri.Features.ListRequests;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands;

/// <summary>The request verbs (API P4 unit H): request, requests, decide, needs-you. Exit 0 ok, 1 while the views rebuild or stall or when Sindri isn't running, 2 refused, invalid or any other error; --json is the route's body.</summary>
public sealed class CliRequestVerbsTests : IDisposable
{
    private static readonly FakeControlPlaneProbe _probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private static readonly AsOf _fresh = new(5, 5, KnownInstants.Epoch, Rebuilding: false, Stalled: false);
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static IHumanTokenStore Human => InMemoryHumanTokenStore.WithToken();

    private static IHumanTokenStore NoToken => new InMemoryHumanTokenStore();   // a session: no readable token

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    private string Out => _out.ToString().ReplaceLineEndings("\n");

    private static string Body<T>(T value) => Encoding.UTF8.GetString(AwJson.Serialize(value, SliceHarness.Json)) + "\n";

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_request_posts_the_kind_item_and_text_and_prints_both_ids()
    {
        var filed = new RequestFiledResponse(new CommandAccepted(new SequentialIdGenerator().New<CommandId>(), Sequence: 6), A.FirstRequestId, A.FirstDecisionId);
        var api = new RoutedApi().On("/sindri/requests", HttpStatusCode.OK, filed);

        var code = await Verbs(api, NoToken).Run(["request", "rescope", "--item", A.FirstItemId.Value, "Split", "the", "work."], Token);

        Assert.Equal((0, $"{A.FirstRequestId.Value} filed; {A.FirstDecisionId.Value} waits for a human (sequence 6)\n"), (code, Out));
        var call = Assert.Single(api.Calls);
        Assert.Equal(("POST", "session", $$"""{"kind":"rescope","text":"Split the work.","item":"{{A.FirstItemId.Value}}"}"""), (call.Method, call.Headers[SindriHeaders.Caller], call.Body));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_request_without_an_item_sends_null_and_json_is_the_route_body()
    {
        var filed = new RequestFiledResponse(new CommandAccepted(new SequentialIdGenerator().New<CommandId>(), Sequence: 6), A.FirstRequestId, A.FirstDecisionId);
        var api = new RoutedApi().On("/sindri/requests", HttpStatusCode.OK, filed);

        var code = await Verbs(api, NoToken).Run(["request", "note", "All", "done.", "--json"], Token);

        Assert.Equal((0, Body(filed)), (code, Out));
        Assert.Equal("""{"kind":"note","text":"All done.","item":null}""", Assert.Single(api.Calls).Body);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("request")]
    [InlineData("request rescope")]
    [InlineData("request bogus Split it")]
    [InlineData("request rescope --item not-an-item Split it")]
    public async Task Cli_request_with_bad_arguments_exits_2_and_calls_nothing(string line)
    {
        var api = new RoutedApi();

        var code = await Verbs(api, NoToken).Run(line.Split(' '), Token);

        Assert.Equal((2, 0), (code, api.Calls.Count));
        Assert.StartsWith("SND-CLI-003 ", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_request_prints_the_servers_refusal_and_exits_2()
    {
        var api = new RoutedApi().OnRaw("/sindri/requests", HttpStatusCode.TooManyRequests, """{"type":"about:blank","title":"SND-REQ-002","status":429,"detail":"20 open requests already"}""");

        var code = await Verbs(api, NoToken).Run(["request", "note", "Again"], Token);

        Assert.Equal((2, "SND-REQ-002 20 open requests already\n"), (code, _error.ToString().ReplaceLineEndings("\n")));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_requests_prints_one_line_per_request_with_its_text_and_filters()
    {
        var view = new RequestView(A.FirstRequestId, RequestKind.Rescope, A.FirstItemId, RepoName.From("demo"), "Split the work.", "session:cli", RequestState.Open, A.FirstDecisionId, KnownInstants.Epoch);
        var api = new RoutedApi().On("/sindri/requests", HttpStatusCode.OK, new ListRequestsResponse([view], Empty: null, _fresh));

        var code = await Verbs(api, Human).Run(["requests", "--state", "open", "--item", A.FirstItemId.Value], Token);

        Assert.Equal((0, "?state=open&item=" + A.FirstItemId.Value), (code, api.Calls[^1].Query));
        Assert.Equal($"{A.FirstRequestId.Value}  rescope  open  [demo]  {A.FirstItemId.Value}  by session:cli  since 2026-01-01T00:00:00Z  decision {A.FirstDecisionId.Value}\n  Split the work.\n", Out);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_requests_says_the_empty_sentence_and_exits_1_while_stalled()
    {
        var stalled = new AsOf(3, 7, KnownInstants.Epoch, Rebuilding: false, Stalled: true);
        var list = new ListRequestsResponse([], EmptyText.Stalled(3), stalled);
        var api = new RoutedApi().On("/sindri/requests", HttpStatusCode.OK, list);

        var code = await Verbs(api, Human).Run(["requests", "--json"], Token);

        Assert.Equal((1, Body(list)), (code, Out));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Cli_requests_exits_1_while_the_views_rebuild_or_stall(bool rebuilding, bool stalled)
    {
        var asOf = new AsOf(3, 7, KnownInstants.Epoch, rebuilding, stalled);
        var api = new RoutedApi().On("/sindri/requests", HttpStatusCode.OK, new ListRequestsResponse([], EmptyText.Rebuilding(3, 7), asOf));

        var code = await Verbs(api, Human).Run(["requests"], Token);

        Assert.Equal((1, EmptyText.Rebuilding(3, 7) + "\n"), (code, Out));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_requests_indents_every_line_of_a_multi_line_text()
    {
        var view = new RequestView(A.FirstRequestId, RequestKind.Note, Item: null, Repo: null, "first\nsecond\n" + A.FirstRequestId.Value + "  rescope  open  forged line", "session:cli", RequestState.Open, A.FirstDecisionId, KnownInstants.Epoch);
        var api = new RoutedApi().On("/sindri/requests", HttpStatusCode.OK, new ListRequestsResponse([view], Empty: null, _fresh));

        _ = await Verbs(api, Human).Run(["requests"], Token);

        Assert.EndsWith("\n  first\n  second\n  " + A.FirstRequestId.Value + "  rescope  open  forged line\n", Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_requests_text_with_none_prints_the_sentence()
    {
        var api = new RoutedApi().On("/sindri/requests", HttpStatusCode.OK, new ListRequestsResponse([], "No requests.", _fresh));

        var code = await Verbs(api, Human).Run(["requests"], Token);

        Assert.Equal((0, "No requests.\n"), (code, Out));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_decide_posts_the_choice_and_note_as_the_human()
    {
        var api = new RoutedApi().On("/sindri/decisions/" + A.FirstDecisionId.Value, HttpStatusCode.OK, new CommandAccepted(new SequentialIdGenerator().New<CommandId>(), Sequence: 9));

        var code = await Verbs(api, Human).Run(["decide", A.FirstDecisionId.Value, "decline", "--note", "Not now."], Token);

        Assert.Equal((0, $"{A.FirstDecisionId.Value} decided: decline (sequence 9)\n"), (code, Out));
        var call = Assert.Single(api.Calls);
        Assert.Equal(("POST", "human", """{"choice":"decline","note":"Not now."}"""), (call.Method, call.Headers[SindriHeaders.Caller], call.Body));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_decide_json_is_the_route_body()
    {
        var accepted = new CommandAccepted(new SequentialIdGenerator().New<CommandId>(), Sequence: 9);
        var api = new RoutedApi().On("/sindri/decisions/" + A.FirstDecisionId.Value, HttpStatusCode.OK, accepted);

        var code = await Verbs(api, Human).Run(["decide", A.FirstDecisionId.Value, "accept", "--json"], Token);

        Assert.Equal((0, Body(accepted)), (code, Out));
        Assert.Equal("""{"choice":"accept","note":null}""", Assert.Single(api.Calls).Body);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_decide_as_a_session_prints_the_servers_refusal_and_exits_2()
    {
        var api = new RoutedApi().OnRaw("/sindri/decisions/" + A.FirstDecisionId.Value, HttpStatusCode.Forbidden, """{"type":"about:blank","title":"SND-AUTH-002","status":403,"detail":"only a human decides"}""");

        var code = await Verbs(api, NoToken).Run(["decide", A.FirstDecisionId.Value, "accept"], Token);

        Assert.Equal(2, code);
        Assert.StartsWith("SND-AUTH-002 only a human decides", _error.ToString(), StringComparison.Ordinal);
        Assert.Equal("session", Assert.Single(api.Calls).Headers[SindriHeaders.Caller]);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("decide")]
    [InlineData("decide DEC-00000000000000000000000000000001")]
    [InlineData("decide DEC-00000000000000000000000000000001 Not_An_Option")]
    [InlineData("decide DEC-00000000000000000000000000000001 accept extra")]
    public async Task Cli_decide_with_bad_arguments_exits_2_and_calls_nothing(string line)
    {
        var api = new RoutedApi();

        var code = await Verbs(api, Human).Run(line.Split(' '), Token);

        Assert.Equal((2, 0), (code, api.Calls.Count));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_needs_you_prints_each_row_with_its_reason_notice_and_command()
    {
        var row = A.NeedsYouRow().Build();
        var api = new RoutedApi().On("/sindri/needs-you", HttpStatusCode.OK, new GetNeedsYouResponse([row], Empty: null, _fresh));

        var code = await Verbs(api, Human).Run(["needs-you"], Token);

        Assert.Equal((0, $"decision · re-scope  {row.Title}  since 2026-01-01T00:00:00Z\n  {row.Reason}\n  {row.Notice}\n  {row.Cli}\n"), (code, Out));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_needs_you_with_none_prints_the_sentence_and_json_is_the_route_body()
    {
        var list = new GetNeedsYouResponse([], EmptyText.NeedsYou(running: 1, queued: 2), _fresh);
        var api = new RoutedApi().On("/sindri/needs-you", HttpStatusCode.OK, list);

        var text = await Verbs(api, Human).Run(["needs-you"], Token);
        var textOut = Out;
        var json = await Verbs(api, Human).Run(["needs-you", "--json"], Token);

        Assert.Equal((0, 0, EmptyText.NeedsYou(running: 1, queued: 2) + "\n"), (text, json, textOut));
        Assert.EndsWith(Body(list), Out, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Cli_needs_you_exits_1_while_the_views_rebuild_or_stall(bool rebuilding, bool stalled)
    {
        var asOf = new AsOf(3, 7, KnownInstants.Epoch, rebuilding, stalled);
        var api = new RoutedApi().On("/sindri/needs-you", HttpStatusCode.OK, new GetNeedsYouResponse([], EmptyText.Rebuilding(3, 7), asOf));

        var code = await Verbs(api, Human).Run(["needs-you"], Token);

        Assert.Equal((1, EmptyText.Rebuilding(3, 7) + "\n"), (code, Out));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_request_verbs_send_the_ledger_head_as_x_sindri_after_on_reads()
    {
        var api = new RoutedApi().On("/sindri/requests", HttpStatusCode.OK, new ListRequestsResponse([], "No requests.", _fresh))
            .On("/sindri/needs-you", HttpStatusCode.OK, new GetNeedsYouResponse([], "Nothing needs you.", _fresh));

        _ = await Verbs(api, Human).Run(["requests"], Token);
        _ = await Verbs(api, Human).Run(["needs-you"], Token);

        Assert.Equal("7,7", string.Join(',', api.Calls.Where(static c => !string.Equals(c.Path, "/sindri/status", StringComparison.Ordinal)).Select(static c => c.Headers.GetValueOrDefault(SindriHeaders.After))));
    }

    private static DecisionView PendingDecision() => new(A.FirstDecisionId, DecisionKind.AcceptRequest, ActionClass.Decision, "decision · re-scope", A.FirstItemId, ItemVersion: 3, RepoName.From("demo"),
        "Accept this re-scope request?\nSplit the work.", [new OptionView(DecisionOption.From("accept"), "Accept", RejectLike: false, $"sindri decide {A.FirstDecisionId.Value} accept"), new OptionView(DecisionOption.From("decline"), "Decline", RejectLike: true, $"sindri decide {A.FirstDecisionId.Value} decline")],
        DecisionOption.From("accept"), DecisionState.Pending, "session:cli", KnownInstants.Epoch, DecidedBy: null, DecidedAt: null, Choice: null, Note: null, RecordedOnly: true, Notice: "recorded; acted on once the workflow engine runs",
        [ActionId.From("Decide")], $"sindri decide {A.FirstDecisionId.Value} accept", Cleared: null);

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_decisions_prints_one_block_per_decision_with_question_and_command()
    {
        var view = PendingDecision();
        var api = new RoutedApi().On("/sindri/decisions", HttpStatusCode.OK, new ListDecisionsResponse([view], Empty: null, _fresh));

        var code = await Verbs(api, Human).Run(["decisions"], Token);

        Assert.Equal((0, ""), (code, api.Calls[^1].Query));
        Assert.Equal($"{A.FirstDecisionId.Value}  decision · re-scope  pending  [demo]  {A.FirstItemId.Value}  by session:cli  since 2026-01-01T00:00:00Z\n  Accept this re-scope request?\n  Split the work.\n  {view.Notice}\n  {view.Cli}\n", Out);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_decisions_of_a_decided_one_says_who_chose_what_and_no_command()
    {
        var view = PendingDecision() with { State = DecisionState.Decided, DecidedBy = "human", DecidedAt = KnownInstants.Epoch, Choice = DecisionOption.From("decline"), Note = "Not now.", Actions = [], Cleared = "declined" };
        var api = new RoutedApi().On("/sindri/decisions", HttpStatusCode.OK, new ListDecisionsResponse([view], Empty: null, _fresh));

        var code = await Verbs(api, Human).Run(["decisions", "--decided"], Token);

        Assert.Equal((0, "?pending=false"), (code, api.Calls[^1].Query));
        Assert.Equal($"{A.FirstDecisionId.Value}  decision · re-scope  decided  [demo]  {A.FirstItemId.Value}  by session:cli  since 2026-01-01T00:00:00Z\n  Accept this re-scope request?\n  Split the work.\n  decided decline by human at 2026-01-01T00:00:00Z: Not now.\n  {view.Notice}\n", Out);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_decisions_indents_the_later_lines_of_a_multi_line_note()
    {
        var view = PendingDecision() with { State = DecisionState.Decided, DecidedBy = "human", DecidedAt = KnownInstants.Epoch, Choice = DecisionOption.From("decline"), Note = "Not now.\nforged line", Actions = [], Cleared = "declined" };
        var api = new RoutedApi().On("/sindri/decisions", HttpStatusCode.OK, new ListDecisionsResponse([view], Empty: null, _fresh));

        _ = await Verbs(api, Human).Run(["decisions", "--decided"], Token);

        Assert.Contains("  decided decline by human at 2026-01-01T00:00:00Z: Not now.\n    forged line\n", Out, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("--pending", "?pending=true")]
    [InlineData("--decided", "?pending=false")]
    public async Task Cli_decisions_maps_the_state_flags_to_the_routes_pending_filter(string flag, string query)
    {
        var api = new RoutedApi().On("/sindri/decisions", HttpStatusCode.OK, new ListDecisionsResponse([], "No pending decisions.", _fresh));

        var code = await Verbs(api, Human).Run(["decisions", flag, "--item", A.FirstItemId.Value], Token);

        Assert.Equal((0, query + "&item=" + A.FirstItemId.Value, "No pending decisions.\n"), (code, api.Calls[^1].Query, Out));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_decisions_passes_only_the_item_when_given_alone()
    {
        var api = new RoutedApi().On("/sindri/decisions", HttpStatusCode.OK, new ListDecisionsResponse([], "No decisions.", _fresh));

        _ = await Verbs(api, Human).Run(["decisions", "--item", A.FirstItemId.Value], Token);

        Assert.Equal("?item=" + A.FirstItemId.Value, api.Calls[^1].Query);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_decisions_json_is_the_route_body()
    {
        var list = new ListDecisionsResponse([PendingDecision()], Empty: null, _fresh);
        var api = new RoutedApi().On("/sindri/decisions", HttpStatusCode.OK, list);

        var code = await Verbs(api, Human).Run(["decisions", "--pending", "--json"], Token);

        Assert.Equal((0, Body(list)), (code, Out));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Cli_decisions_exits_1_while_the_views_rebuild_or_stall(bool rebuilding, bool stalled)
    {
        var asOf = new AsOf(3, 7, KnownInstants.Epoch, rebuilding, stalled);
        var api = new RoutedApi().On("/sindri/decisions", HttpStatusCode.OK, new ListDecisionsResponse([], EmptyText.Rebuilding(3, 7), asOf));

        var code = await Verbs(api, Human).Run(["decisions"], Token);

        Assert.Equal((1, EmptyText.Rebuilding(3, 7) + "\n"), (code, Out));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("decisions --pending --decided")]
    [InlineData("decisions --item not-an-item")]
    [InlineData("decisions extra")]
    [InlineData("decisions --state open")]
    public async Task Cli_decisions_with_bad_arguments_exits_2_and_calls_nothing(string line)
    {
        var api = new RoutedApi();

        var code = await Verbs(api, Human).Run(line.Split(' '), Token);

        Assert.Equal((2, 0), (code, api.Calls.Count));
        Assert.StartsWith("SND-CLI-003 ", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_decisions_sends_the_ledger_head_as_x_sindri_after()
    {
        var api = new RoutedApi().On("/sindri/decisions", HttpStatusCode.OK, new ListDecisionsResponse([], "No decisions.", _fresh));

        _ = await Verbs(api, Human).Run(["decisions"], Token);

        Assert.Equal("7", api.Calls.Single(static c => c.Path == "/sindri/decisions").Headers.GetValueOrDefault(SindriHeaders.After));
    }

    [Fact, Trait("Lane", "unit")]
    public void Usage_names_the_five_verbs_as_the_catalog_does()
    {
        var forms = new[]
        {
            "sindri request <kind> [--item <item>] <text> [--json] [--no-start]",
            "sindri requests [--state <s>] [--item <item>] [--json] [--no-start]",
            "sindri decisions [--pending | --decided] [--item <item>] [--json] [--no-start]",
            "sindri decide <decisionId> <choice> [--note <note>] [--json] [--no-start]",
            "sindri needs-you [--json] [--no-start]",
        };

        Assert.All(forms, f => Assert.Contains(f, Usage.Text, StringComparison.Ordinal));
    }

    private RequestVerbs Verbs(RoutedApi api, IHumanTokenStore tokens)
    {
        var client = new ControlPlaneClient(_probe, SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), tokens, _ => ValueTask.FromResult((RepoName?)null)), handler: api);
        return new RequestVerbs(client, new Output(_out, _error, SliceHarness.Json), (_, _) => Task.FromResult<Kernel.Error?>(null));
    }
}
