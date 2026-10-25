using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The S-4 context verbs over a scripted API: what they send, what they print and the exit codes (0 ok, 1 attention, 2 tool error, 75 the heavy lock).</summary>
public sealed class ContextReviewVerbsTests : IDisposable
{
    private static readonly FakeControlPlaneProbe _probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static (HttpStatusCode, string) Ok(string body) => (HttpStatusCode.OK, body);

    private static (HttpStatusCode, string) Problem(HttpStatusCode status, string code, string detail) =>
        (status, string.Create(System.Globalization.CultureInfo.InvariantCulture, $$"""{"type":"x","title":"{{code}}","status":{{(int)status}},"detail":"{{detail}}"}"""));

    private async Task<int> Run(ScriptedApi api, bool interactive, string typed, params string[] args)
    {
        using var client = new ControlPlaneClient(_probe, SliceHarness.Json, handler: api);
        var verbs = new ContextMapVerbs(client, new Output(_out, _error, SliceHarness.Json), interactive, new StringReader(typed));
        return await verbs.Run(args, Token);
    }

    private async Task<int> Lens(ScriptedApi api, params string[] args) => await LensAs(tokens: null, api, args);

    /// <summary>The lens verbs as a human (<paramref name="tokens"/> holds the token) or as a session.</summary>
    private async Task<int> LensAs(IHumanTokenStore? tokens, ScriptedApi api, params string[] args)
    {
        var headers = tokens is null ? null : CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), tokens, static _ => ValueTask.FromResult<Core.Ids.RepoName?>(null));
        using var client = new ControlPlaneClient(_probe, SliceHarness.Json, headers, handler: api);
        return await new ContextLensVerbs(client, new Output(_out, _error, SliceHarness.Json)).Run(args, Token);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Promote_is_sent_once_with_no_challenge_and_prints_the_result()
    {
        var api = new ScriptedApi(
            Ok("""{"draft":"v14@0a1b2c3","promoted":{"version":{"number":14,"repo":"aw-split-panes","commit":null,"keyVersion":1,"created":"2026-10-15T00:00:00Z","status":"current"},"retained":null},"sequence":7}"""));

        var exit = await Run(api, interactive: true, string.Empty, "promote");

        Assert.True(exit == 0, _error.ToString());
        var (_, route, body) = Assert.Single(api.Calls);
        Assert.Equal("/sindri/context/map/promote", route);
        Assert.DoesNotContain("answer", body, StringComparison.Ordinal);
        Assert.Contains("Promoted v14@0a1b2c3 to current", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Promote_without_a_terminal_is_sent_all_the_same()
    {
        var api = new ScriptedApi(
            Ok("""{"draft":"v14@0a1b2c3","promoted":{"version":{"number":14,"repo":"aw-split-panes","commit":null,"keyVersion":1,"created":"2026-10-15T00:00:00Z","status":"current"},"retained":null},"sequence":7}"""));

        var exit = await Run(api, interactive: false, string.Empty, "promote");

        Assert.True(exit == 0, _error.ToString());
        _ = Assert.Single(api.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Promote_with_undecided_items_is_SND_CONTEXT_046_exit_1()
    {
        var api = new ScriptedApi(Problem(HttpStatusCode.Conflict, "SND-CONTEXT-046", "draft v14@0a1b2c3 has 3 undecided review items"));

        var exit = await Run(api, interactive: true, string.Empty, "promote");

        Assert.Equal(1, exit);
        Assert.Contains("SND-CONTEXT-046 ", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_busy_heavy_lock_exits_75_and_an_unreadable_repo_exits_2()
    {
        var busy = await Run(new ScriptedApi(Problem(HttpStatusCode.ServiceUnavailable, "SND-CONTEXT-040", "the heavy-job lock stayed busy")), interactive: true, string.Empty, "review", "--accept-deterministic");
        var unreadable = await Run(new ScriptedApi(Problem(HttpStatusCode.ServiceUnavailable, "SND-CONTEXT-048", "git can't read the repo")), interactive: true, string.Empty, "review", "--accept-deterministic");

        Assert.Equal((75, 2), (busy, unreadable));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_single_decision_is_sent_at_once_and_prints_the_rows_and_what_is_left()
    {
        var api = new ScriptedApi(Ok("""{"result":{"written":["ovr_0123456789abcdef"],"skipped":[],"undecided":4},"sequence":9}"""));

        var exit = await Run(api, interactive: false, string.Empty, "review", "accept", "feature:tabs");

        Assert.Equal(0, exit);
        var (method, route, body) = Assert.Single(api.Calls);
        Assert.Equal(("POST", "/sindri/context/map/review"), (method, route));
        Assert.Contains("\"verb\":\"accept\"", body, StringComparison.Ordinal);
        Assert.Contains("\"node\":\"feature:tabs\"", body, StringComparison.Ordinal);
        Assert.Contains("1 row written, 4 undecided", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_loop_sends_the_bulk_accept_and_each_decision_and_skip_sends_nothing()
    {
        const string Queue = """{"draft":"v14@0a1b2c3","entries":[{"node":"surface:command/close-tab","link":null,"group":"deterministic","label":"Close tab","provenance":"deterministic","confidence":1,"impact":1,"files":[],"parent":null},{"node":"feature:layout","link":null,"group":"model","label":"Layout","provenance":"model","confidence":0.7,"impact":1,"files":[],"parent":null},{"node":"feature:zoom","link":null,"group":"model","label":"Zoom","provenance":"model","confidence":0.6,"impact":0,"files":[],"parent":null}],"decided":0,"proposals":3,"version":{"number":14,"commit":"0a1b2c3"},"counts":{"deterministic":1,"model":2,"orphaned":0},"byKind":{"surface":1},"truncated":{"deterministic":false,"model":false,"orphaned":false}}""";
        const string Done = """{"result":{"written":["ovr_0123456789abcdef"],"skipped":[],"undecided":1},"sequence":9}""";
        var api = new ScriptedApi(Ok(Queue), Ok(Done), Ok(Done));

        var exit = await Run(api, interactive: true, "y\na\ns\n", "review");

        Assert.Equal(0, exit);
        Assert.Equal(3, api.Calls.Count);   // the queue, the bulk accept, the accept; the skip sends nothing
        Assert.Contains("\"verb\":\"accept-deterministic\"", api.Calls[1].Body, StringComparison.Ordinal);
        Assert.Contains("\"node\":\"feature:layout\"", api.Calls[2].Body, StringComparison.Ordinal);
        Assert.Contains("skipped 1", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_refused_bulk_accept_leaves_the_deterministic_items_in_the_walk_and_the_exit_code_not_zero()
    {
        const string Queue = """{"draft":"v14@0a1b2c3","entries":[{"node":"surface:command/close-tab","link":null,"group":"deterministic","label":"Close tab","provenance":"deterministic","confidence":1,"impact":1,"files":[],"parent":null},{"node":"feature:layout","link":null,"group":"model","label":"Layout","provenance":"model","confidence":0.7,"impact":1,"files":[],"parent":null}],"decided":0,"proposals":2,"version":{"number":14,"commit":"0a1b2c3"},"counts":{"deterministic":1,"model":1,"orphaned":0},"byKind":{"surface":1},"truncated":{"deterministic":false,"model":false,"orphaned":false}}""";
        var api = new ScriptedApi(Ok(Queue), Problem(HttpStatusCode.ServiceUnavailable, "SND-CONTEXT-004", "the context database is locked"));

        var exit = await Run(api, interactive: true, "y\nq\n", "review");

        Assert.NotEqual(0, exit);
        Assert.Contains("[1/2] Close tab", _out.ToString(), StringComparison.Ordinal);   // the deterministic item is walked after all
        Assert.Contains("accepted 0", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_loop_offers_the_whole_backlog_counts_only_what_the_server_took_and_says_how_many_it_did_not_show()
    {
        const string Queue = """{"draft":"v14@0a1b2c3","entries":[{"node":"surface:command/close-tab","link":null,"group":"deterministic","label":"Close tab","provenance":"deterministic","confidence":1,"impact":1,"files":[],"parent":null},{"node":"feature:layout","link":null,"group":"model","label":"Layout","provenance":"model","confidence":0.7,"impact":1,"files":[],"parent":null}],"decided":0,"proposals":8,"version":{"number":14,"commit":"0a1b2c3"},"counts":{"deterministic":3,"model":5,"orphaned":0},"byKind":{"surface":3},"truncated":{"deterministic":true,"model":true,"orphaned":false}}""";
        const string Bulk = """{"result":{"written":["ovr_0123456789abcde1","ovr_0123456789abcde2","ovr_0123456789abcde3"],"skipped":[],"undecided":1},"sequence":9}""";
        var api = new ScriptedApi(Ok(Queue), Ok(Bulk), Problem(HttpStatusCode.Conflict, "SND-CONTEXT-047", "feature:layout is not in the review queue"));

        var exit = await Run(api, interactive: true, "y\na\ns\nq\n", "review");

        Assert.Equal(1, exit);
        Assert.Contains("Accept all 3 deterministic", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("accepted 3, rejected 0", _out.ToString(), StringComparison.Ordinal);   // the refused accept of Layout is not counted
        Assert.Contains("skipped 1", _out.ToString(), StringComparison.Ordinal);   // and Layout is asked again, not passed over: the one skip is the answer to that second ask
        Assert.Contains("4 more undecided", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_merge_is_sent_at_once_with_no_challenge()
    {
        var api = new ScriptedApi(Ok("""{"result":{"written":["ovr_0123456789abcdef"],"skipped":[],"undecided":0},"sequence":3}"""));

        var exit = await Run(api, interactive: false, string.Empty, "review", "merge", "feature:a", "--into", "feature:b");

        Assert.True(exit == 0, _error.ToString());
        var (_, _, body) = Assert.Single(api.Calls);
        Assert.Contains("\"verb\":\"merge\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("typed", body, StringComparison.Ordinal);
        Assert.Contains("1 row written", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Lenses_and_filemap_build_their_queries_and_the_filemap_decisions_go_through_review()
    {
        var empty = new ScriptedApi(Problem(HttpStatusCode.NotFound, "SND-CONTEXT-044", "there is no draft map"));

        var lenses = await Lens(empty, "lenses", "feature:tabs.tab-manager", "--draft");
        var filemap = await Lens(new ScriptedApi(Problem(HttpStatusCode.NotFound, "SND-CONTEXT-045", "not a repo")), "filemap", "--repo", "nope", "--path", "src/", "--unmapped");
        var bad = await Lens(new ScriptedApi(Ok("{}")), "filemap", "--path", "src/");

        Assert.Equal(("GET", "/sindri/context/lenses?anchor=feature%3Atabs.tab-manager&draft=true"), (empty.Calls[0].Method, empty.Calls[0].Route));
        Assert.Equal((1, 1, 2), (lenses, filemap, bad));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_context_lenses_takes_several_keys_and_map_version()
    {
        var several = new ScriptedApi(Problem(HttpStatusCode.NotFound, "SND-CONTEXT-120", "map version v99 isn't held"));

        var exit = await Lens(several, "lenses", "feature:a", "feature:b", "--map-version", "v99", "--depth", "2");
        var none = await Lens(new ScriptedApi(Ok("{}")), "lenses", "--map-version", "v1");

        Assert.Equal("/sindri/context/lenses?anchor=feature%3Aa&anchor=feature%3Ab&depth=2&mapVersion=v99", several.Calls[0].Route);
        Assert.Equal((1, 2), (exit, none));   // 120 is attention; no key is a usage error sent nowhere
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_context_lenses_sends_the_hops_flag_as_hops()
    {
        var api = new ScriptedApi(Ok("{}"));

        _ = await Lens(api, "lenses", "feature:a", "--hops", "state,component", "--depth", "1");

        Assert.Equal("/sindri/context/lenses?anchor=feature%3Aa&depth=1&hops=state%2Ccomponent", api.Calls[0].Route);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_context_lenses_refuses_draft_with_map_version_as_006_naming_both_flags()
    {
        var api = new ScriptedApi(Ok("{}"));

        var exit = await Lens(api, "lenses", "feature:a", "--draft", "--map-version", "v1");

        var text = _error.ToString();
        Assert.Equal(1, exit);   // 006 is attention on the CLI, like every other 006 refusal
        Assert.Empty(api.Calls);
        Assert.Contains("SND-CONTEXT-006", text, StringComparison.Ordinal);
        Assert.Contains("--draft", text, StringComparison.Ordinal);
        Assert.Contains("--map-version", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_context_lenses_reads_a_schema_2_answer_with_its_missing_anchors()
    {
        const string Set = """{"schemaVersion":2,"anchors":[{"key":"feature:a","label":"A","kind":"feature","trust":"trusted"},{"key":"feature:b","label":"B","kind":"feature","trust":"trusted"}],"missingAnchors":["nope:x"],"mapVersion":"v2","draft":false,"depth":2,"asOf":"2026-10-14T09:12:40Z","bands":{"intent":{"records":[],"decisions":[],"counts":{"records":0,"decisions":0,"orphaned":null}},"product":{"own":[],"neighbors":[],"affects":[],"counts":{"own":0,"affects":0},"edges":[]},"code":{"files":[],"symbols":{"state":"unavailable","reason":"index not built","items":[]},"counts":{"files":0,"symbols":0,"featuresWithNoCode":0}}},"edges":[],"conflicts":[],"coverage":{"scope":0,"intent":{"covered":0,"of":0},"product":{"confirmed":0,"of":0},"code":{"implemented":0,"of":0,"level":"file","suggested":0},"gaps":[],"byAnchor":{}},"searched":{"anchors":["feature:a","feature:b"],"mapVersion":"v2","depth":2,"hops":["state","dependency","flow"],"sources":[{"id":"map","state":"ok","reason":null,"code":null,"asOf":"2026-10-14T09:00:00Z"},{"id":"onyx","state":"degraded","reason":null,"code":"SND-CONTEXT-010","asOf":null},{"id":"code-index","state":"unavailable","reason":"index-not-built","code":null,"asOf":null}],"bands":{"intent":{"empty":true},"product":{"empty":true},"code":{"empty":true}}},"degraded":[],"cites":{}}""";
        var api = new ScriptedApi(Ok(Set));

        var exit = await Lens(api, "lenses", "feature:a", "feature:b", "nope:x");

        Assert.True(exit == 0, _error.ToString());
        Assert.Contains("2 anchors", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("missing anchor: nope:x", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("onyx degraded (SND-CONTEXT-010)", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Lenses_send_the_depth_and_a_human_cli_fences_the_untrusted_labels_a_session_cli_leaves_them()
    {
        const string Lenses = """{"schemaVersion":1,"anchor":{"key":"feature:a","label":"A","kind":"feature","trust":"trusted"},"mapVersion":"v1","draft":false,"depth":3,"asOf":"2026-10-14T09:12:40Z","bands":{"intent":{"records":[{"key":"record:linear:issue/1","label":"Spec <x>","source":"linear","mentions":["feature:a"],"trust":"untrusted","state":"unchecked","cites":[],"kind":"record"}],"decisions":[],"counts":{"records":1,"decisions":0,"orphaned":null}},"product":{"own":[],"neighbors":[],"affects":[],"counts":{"own":0,"affects":0},"edges":[]},"code":{"files":[],"symbols":{"state":"unavailable","reason":"index not built","items":[]},"counts":{"files":0,"symbols":0,"featuresWithNoCode":0}}},"edges":[],"conflicts":[],"coverage":{"scope":1,"intent":{"covered":1,"of":1},"product":{"confirmed":1,"of":1},"code":{"implemented":0,"of":1,"level":"file","suggested":0},"gaps":[]},"searched":{"anchors":["feature:a"],"mapVersion":"v1","depth":3,"hops":["state"],"sources":[{"id":"map","state":"ok","reason":null,"code":null,"asOf":"2026-10-14T09:00:00Z"}],"bands":{"intent":{"empty":false},"product":{"empty":true},"code":{"empty":true}}},"degraded":[],"cites":{}}""";
        var human = new ScriptedApi(Ok(Lenses));
        var humanExit = await LensAs(InMemoryHumanTokenStore.WithToken(), human, "lenses", "feature:a", "--depth", "3");
        var humanText = _out.ToString();
        _out.GetStringBuilder().Clear();
        var session = new ScriptedApi(Ok(Lenses.Replace("\"label\":\"Spec <x>\"", "\"label\":\"<untrusted source=\\\"map\\\">Spec &lt;x&gt;</untrusted>\"", StringComparison.Ordinal)));   // what the server sends a session

        var sessionExit = await LensAs(new InMemoryHumanTokenStore(), session, "lenses", "feature:a");

        Assert.True(humanExit == 0 && sessionExit == 0, _error.ToString());
        Assert.Equal("/sindri/context/lenses?anchor=feature%3Aa&depth=3", human.Calls[0].Route);
        Assert.Contains("<untrusted source=\"map\">Spec &lt;x&gt;</untrusted>", humanText, StringComparison.Ordinal);
        Assert.Contains("<untrusted source=\"map\">Spec &lt;x&gt;</untrusted>", _out.ToString(), StringComparison.Ordinal);
        _ = Assert.Single(System.Text.RegularExpressions.Regex.Matches(_out.ToString(), "<untrusted", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1)));   // the server fenced it for a session; the CLI doesn't fence again
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Map_status_with_an_anchor_sends_it_and_prints_the_coverage()
    {
        const string Status = """{"version":{"number":1,"repo":"aw-split-panes","commit":null,"keyVersion":1,"created":"2026-10-15T00:00:00Z","status":"current"},"counts":{"nodes":10,"edges":13,"aliases":0,"decisions":2,"overrides":1},"orphaned":1,"retained":0,"scopedTo":null,"coverage":{"scope":6,"intent":{"covered":2,"of":6},"product":{"confirmed":6,"of":6},"code":{"implemented":2,"of":6,"level":"file","suggested":1},"gaps":[]}}""";
        var api = new ScriptedApi(Ok(Status));

        var exit = await Run(api, interactive: false, string.Empty, "status", "--anchor", "feature:tabs.tab-manager");

        Assert.True(exit == 0, _error.ToString());
        Assert.Equal("/sindri/context/map?anchor=feature%3Atabs.tab-manager", api.Calls[0].Route);
        Assert.Contains("coverage: scope 6 · intent 2 of 6", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_review_queue_and_history_send_the_limit_flag()
    {
        var queue = new ScriptedApi(Problem(HttpStatusCode.Conflict, "SND-CONTEXT-044", "none"));
        var history = new ScriptedApi(Problem(HttpStatusCode.Conflict, "SND-CONTEXT-044", "none"));

        _ = await Run(queue, interactive: false, string.Empty, "review", "--list", "--limit", "5");
        _ = await Run(history, interactive: false, string.Empty, "review", "--history", "--limit", "7");

        Assert.Equal("/sindri/context/map/review?limit=5", queue.Calls[0].Route);
        Assert.Equal("/sindri/context/map/review/history?limit=7", history.Calls[0].Route);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Propose_reads_a_camelCase_file_sends_it_and_prints_each_surfaces_standing()
    {
        var file = Path.Combine(Path.GetTempPath(), $"aw-propose-{RealTime.NewGuid():N}.json");
        await File.WriteAllTextAsync(file, """{"run":"scope-1","repo":"work-app","surfaces":[{"name":"Zoom","kind":"feature"},{"name":"Tab manager","key":"feature:tabs.tab-manager"}]}""", Token);
        try
        {
            var api = new ScriptedApi(Ok("""{"result":{"draft":{"number":14,"repo":"work-app","commit":null,"keyVersion":1,"created":"2026-10-15T00:00:00Z","status":"draft"},"added":1,"existing":1,"rejected":0},"sequence":13,"surfaces":[{"surface":"Zoom","node":"feature:zoom","status":"new"},{"surface":"Tab manager","node":"feature:tabs.tab-manager","status":"mapped"}]}"""));

            var exit = await Run(api, interactive: false, string.Empty, "propose", file);

            Assert.True(exit == 0, _error.ToString());
            Assert.Contains("\"run\":\"scope-1\"", api.Calls[0].Body, StringComparison.Ordinal);
            Assert.Contains("Proposed into draft v14: 1 added, 1 already mapped, 0 rejected earlier; recorded as event 13", _out.ToString(), StringComparison.Ordinal);
            Assert.Contains("new       <untrusted source=\"map\">Zoom</untrusted> → feature:zoom", _out.ToString(), StringComparison.Ordinal);
            Assert.Contains("mapped    <untrusted source=\"map\">Tab manager</untrusted> → feature:tabs.tab-manager", _out.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(file);
        }
    }
}
