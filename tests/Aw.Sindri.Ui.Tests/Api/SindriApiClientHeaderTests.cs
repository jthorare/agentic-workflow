using System.Net;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Api.Projects;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>The headers the app sends (UI spec §9.1, API §2.4, §6): the human token, and X-Sindri-After once after a command.</summary>
[Lane(TestLane.Unit)]
public sealed class SindriApiClientHeaderTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // API P1: serve mints <home>/auth/human.token; a human call without it is refused, so the app sends it on every call.
    [Fact]
    public async Task Every_call_carries_this_starts_human_token_and_the_api_lets_it_in()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi()) { RequiredToken = "tok-1" };
        var client = new SindriApiClient(new HttpClient(handler), new InMemoryHumanToken("tok-1\n"), TimeProvider.System);

        Assert.True((await client.Ping(_api, Token)).IsSuccess);
        Assert.True((await client.Commands(_api, Token)).IsSuccess);
        Assert.True((await client.RebuildViews(_api, Token)).IsSuccess);

        Assert.All(handler.Requests, r => Assert.Equal("tok-1", r.Token));
    }

    // A restarted control plane mints a new token: the app reads the source before each call, so it never sends a stale one.
    [Fact]
    public async Task A_new_token_is_used_from_the_next_call()
    {
        var tokens = new InMemoryHumanToken("tok-1");
        var handler = new FakeSindriHandler(new FakeSindriApi());
        var client = new SindriApiClient(new HttpClient(handler), tokens, TimeProvider.System);

        _ = await client.Ping(_api, Token);
        tokens.Write("tok-2");
        _ = await client.Ping(_api, Token);

        Assert.Equal(["tok-1", "tok-2"], handler.Requests.Select(r => r.Token), StringComparer.Ordinal);
    }

    // No token file (or one the platform can't read): the app sends none, and shows the API's refusal as served.
    [Fact]
    public async Task Without_a_token_the_refusal_is_the_apis_SND_AUTH_001_and_its_fix()
    {
        var tokens = new InMemoryHumanToken();
        var handler = new FakeSindriHandler(new FakeSindriApi()) { RequiredToken = "tok-1" };
        var client = new SindriApiClient(new HttpClient(handler), tokens, TimeProvider.System);

        var status = await client.Status(_api, Token);
        tokens.FailReads(new IOException("denied"));
        var again = await client.Status(_api, Token);

        Assert.Equal("SND-AUTH-001", status.Error.Code);
        // The detail is the API's, as served (keyed off the golden, never a copy of its sentence, which P2 rewords).
        var served = System.Text.Json.JsonDocument.Parse(GoldenFiles.Api("status", "human-no-token")).RootElement.GetProperty("detail").GetString();
        Assert.Equal(served, status.Error.Message);
        Assert.Equal("SND-AUTH-001", again.Error.Code);
        Assert.All(handler.Requests, r => Assert.Null(r.Token));
    }

    // API §2.4: after an accepted command the next query carries its sequence once; the stream and commands never do.
    [Fact]
    public async Task The_query_after_an_accepted_command_waits_for_its_sequence_once()
    {
        var server = new FakeSindriApi { StreamsEndAtOnce = true };
        var handler = new FakeSindriHandler(server);
        var client = new SindriApiClient(new HttpClient(handler), new InMemoryHumanToken("tok-1"), TimeProvider.System);

        _ = await client.Status(_api, Token);
        _ = await client.RebuildViews(_api, Token);
        _ = await client.Events(_api, 0, Token).ToListAsync(Token);
        _ = await client.Services(_api, Token);
        _ = await client.Status(_api, Token);

        Assert.Equal(
            [("GET", "/sindri/status", null), ("POST", "/sindri/views/rebuild", null), ("GET", "/sindri/events", null), ("GET", "/sindri/services", "43"), ("GET", "/sindri/status", null)],
            handler.Requests.Select(r => (r.Method, r.Path, r.After)));
    }

    // API §2.4 for CreateItem: its answer nests the accepted command, whose sequence the next query waits for.
    [Fact]
    public async Task The_query_after_a_created_item_waits_for_its_sequence()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi());
        var client = new SindriApiClient(new HttpClient(handler), new InMemoryHumanToken("tok-1"), TimeProvider.System);

        _ = await client.CreateItem(_api, new CreateItemRequest("A title", Repo: null, Body: null, Link: null), Token);
        _ = await client.Queue(_api, Token);
        _ = await client.CloseItem(_api, A.NewItem, new CloseItemRequest("done", 1), Token);
        _ = await client.Item(_api, A.NewItem, Token);

        Assert.Equal(
            [("POST", "/sindri/items", null), ("GET", "/sindri/queue", "58"), ("POST", "/sindri/items/" + A.NewItem + "/close", null), ("GET", "/sindri/items/" + A.NewItem, "59")],
            handler.Requests.Select(r => (r.Method, r.Path, r.After)));
    }

    // API §2.4 for a job start: the 202 is answered when job.started is in the ledger, the jobs view is projected later, so the first job read waits for the start's sequence.
    [Fact]
    public async Task The_job_read_after_a_job_start_waits_for_its_sequence()
    {
        const string JobId = "JOB-00000000000000000000000000000007";
        var fake = new FakeSindriApi
        {
            SetContextPagesAnswer = new RouteAnswer(HttpStatusCode.Accepted, $$"""{"accepted":{"commandId":"00000000-0000-0000-0000-000000000009","sequence":12},"jobId":"{{JobId}}"}"""),
        };
        var handler = new FakeSindriHandler(fake);
        var client = new SindriApiClient(new HttpClient(handler), new InMemoryHumanToken("tok-1"), TimeProvider.System);

        var started = await client.SetContextPages(_api, new PagesSetBody(IfHash: "h0"), Token);
        _ = await client.Job(_api, started.Value.JobId, Token);

        Assert.Equal(
            [("POST", "/sindri/context/pages", null), ("GET", "/sindri/jobs/" + JobId, "12")],
            handler.Requests.Select(r => (r.Method, r.Path, r.After)));
    }

    // The same read-your-writes rule for every other answer that nests an accepted command: the next view query waits for its sequence.
    [Fact]
    public async Task The_job_read_after_an_approve_all_start_waits_for_its_sequence()
    {
        const string JobId = "JOB-0000000000000000000000000000000e";
        var fake = new FakeSindriApi
        {
            ApproveAllStartAnswer = new RouteAnswer(HttpStatusCode.Accepted, $$"""{"accepted":{"commandId":"00000000-0000-0000-0000-000000000009","sequence":21},"jobId":"{{JobId}}"}"""),
        };
        var handler = new FakeSindriHandler(fake);
        var client = new SindriApiClient(new HttpClient(handler), new InMemoryHumanToken("tok-1"), TimeProvider.System);

        var started = await client.ApproveAll(_api, new ApproveAllRequest(), Token);
        _ = await client.ApproveAllJob(_api, started.Value.JobId, Token);

        Assert.Equal("21", handler.Requests[^1].After);
        Assert.Equal("GET", handler.Requests[^1].Method);
    }

    [Fact]
    public async Task The_query_after_a_job_resume_waits_for_its_sequence() =>
        Assert.Equal("31", await AfterOfQueryFollowing(
            """{"accepted":{"commandId":"00000000-0000-0000-0000-000000000009","sequence":31}}""",
            c => c.ResumeJob(_api, "JOB-00000000000000000000000000000001", Token)));

    [Fact]
    public async Task The_query_after_a_project_change_waits_for_its_sequence() =>
        Assert.Equal("41", await AfterOfQueryFollowing(ProjectAccepted(41), c => c.ChangeProject(_api, "prj_1", new ProjectChange(Version: 1, Name: "New"), Token)));

    [Fact]
    public async Task The_query_after_a_project_detach_waits_for_its_sequence() =>
        Assert.Equal("42", await AfterOfQueryFollowing(ProjectAccepted(42), c => c.DetachProject(_api, "prj_1", new ProjectDetach(Version: 1), Token)));

    [Fact]
    public async Task The_query_after_a_project_hide_waits_for_its_sequence() =>
        Assert.Equal("43", await AfterOfQueryFollowing(ProjectAccepted(43), c => c.HideProject(_api, new ProjectHide("linear:project/abc", Hidden: true, Version: 0), Token)));

    [Fact]
    public async Task The_query_after_a_project_attach_waits_for_its_sequence() =>
        Assert.Equal("44", await AfterOfQueryFollowing(ProjectAccepted(44), c => c.AttachProject(_api, new ProjectAttach(Name: "Docs"), Token)));

    [Fact]
    public async Task The_query_after_an_item_open_waits_for_its_sequence() =>
        Assert.Equal("51", await AfterOfQueryFollowing(
            """{"accepted":{"commandId":"00000000-0000-0000-0000-000000000009","sequence":51},"launch":"LCH-1","target":"tmux"}""",
            c => c.OpenItem(_api, A.NewItem, new OpenItemRequest("session", 1), Token)));

    private static string ProjectAccepted(long sequence) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $$"""{"accepted":{"commandId":"cmd_x","sequence":{{sequence}}},"project":"prj_1","version":2,"name":"New"}""");

    // Sends one command that is answered with the given body, then a status query; returns the After header that query carried.
    private static async Task<string?> AfterOfQueryFollowing(string commandAnswer, Func<SindriApiClient, Task> command)
    {
        var handler = new AnswerOnceHandler(commandAnswer);
        var client = new SindriApiClient(new HttpClient(handler), new InMemoryHumanToken("tok-1"), TimeProvider.System);

        await command(client);
        _ = await client.Queue(_api, Token);

        return handler.QueryAfter;
    }

    /// <summary>Answers the first (POST) call with the given JSON and every other call with an empty object, keeping the X-Sindri-After of the last GET.</summary>
    private sealed class AnswerOnceHandler(string commandAnswer) : HttpMessageHandler
    {
        public string? QueryAfter { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                QueryAfter = request.Headers.TryGetValues("X-Sindri-After", out var values) ? values.Single() : null;
            }

            var body = request.Method == HttpMethod.Post ? commandAnswer : "{}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") });
        }
    }

    // A command is a human's from the UI like every call: never X-Sindri-Terminal (protected stays at the CLI), never Origin.
    [Fact]
    public async Task A_command_says_human_from_the_ui_and_never_terminal_or_origin()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi());
        var client = new SindriApiClient(new HttpClient(handler), new InMemoryHumanToken("tok-1"), TimeProvider.System);

        _ = await client.RebuildViews(_api, Token);

        Assert.Equal(new SeenRequest("/sindri/views/rebuild", "human", "ui", Terminal: null, Origin: null, Token: "tok-1", After: null, Method: "POST"), Assert.Single(handler.Requests));
    }

    // UI spec §4.1 Error, API §3.6: a command Sindri refuses (here 409 SND-API-010 while views rebuild) is Sindri's code
    // and its detail as served, so the control shows them inline; only SND-AUTH-001 is retried, keyed off the code.
    [Fact]
    public async Task A_refused_command_is_sindris_code_and_detail_and_isnt_retried()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi())
        {
            Override = (HttpStatusCode.Conflict, RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-API-010", "Views are rebuilding (fix: retry after the rebuild)").Body),
        };
        var client = new SindriApiClient(new HttpClient(handler), new InMemoryHumanToken("tok-1"), TimeProvider.System);

        var rebuild = await client.RebuildViews(_api, Token);

        Assert.Equal(new Error("SND-API-010", ErrorKind.Conflict, "Views are rebuilding (fix: retry after the rebuild)"), rebuild.Error);
        _ = Assert.Single(handler.Requests);
    }

    // F2 (arch-0, corr-0): X-Sindri-After belongs to the view queries (status, services). A ping or the catalog never
    // carries it, and never uses it up.
    [Fact]
    public async Task Only_status_and_services_carry_the_sequence_and_a_ping_or_the_catalog_does_not_use_it_up()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi());
        var client = new SindriApiClient(new HttpClient(handler), new InMemoryHumanToken("tok-1"), TimeProvider.System);

        _ = await client.RebuildViews(_api, Token);
        _ = await client.Ping(_api, Token);
        _ = await client.Commands(_api, Token);
        _ = await client.Services(_api, Token);
        _ = await client.Status(_api, Token);

        Assert.Equal(
            [("POST", null), ("GET", null), ("GET", null), ("GET", "43"), ("GET", null)],
            handler.Requests.Select(r => (r.Method, r.After)));
    }

    // API §3.8 / §2.4: the instance list and an instance are view reads, so after an accepted command the first of them
    // carries its sequence and uses it up; a fact and a recipe are not view reads and never carry it.
    [Fact]
    public async Task The_first_workflow_view_read_after_a_command_carries_the_sequence_and_the_rest_do_not()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi());
        var client = new SindriApiClient(new HttpClient(handler), new InMemoryHumanToken("tok-1"), TimeProvider.System);
        _ = await client.RebuildViews(_api, Token);

        _ = await client.Instances(_api, new InstanceQuery(), Token);
        _ = await client.Instance(_api, "WFI-1", Token);
        _ = await client.InstanceFact(_api, "WFI-1", "fact:a", Token);
        _ = await client.Recipe(_api, "workflow.project", hash: null, Token);

        Assert.Equal(["43", null, null, null], handler.Requests.Skip(1).Select(r => r.After), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_fact_and_a_recipe_never_carry_the_sequence_or_use_it_up_for_the_instance_read_after_them()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi());
        var client = new SindriApiClient(new HttpClient(handler), new InMemoryHumanToken("tok-1"), TimeProvider.System);
        _ = await client.RebuildViews(_api, Token);

        _ = await client.InstanceFact(_api, "WFI-1", "fact:a", Token);
        _ = await client.Recipe(_api, "workflow.project", hash: null, Token);
        _ = await client.Instance(_api, "WFI-1", Token);

        Assert.Equal([null, null, "43"], handler.Requests.Skip(1).Select(r => r.After), StringComparer.Ordinal);
    }

    // H3: the model answers views-behind only for a request that carries the header, so a client that forgot to send it fails.
    [Fact]
    public async Task A_view_read_without_the_header_is_never_held_whatever_the_model_remembers()
    {
        var server = new FakeSindriApi { Lag = ViewsLag.Behind };
        var client = new SindriApiClient(new HttpClient(new FakeSindriHandler(server)), new InMemoryHumanToken("tok-1"), TimeProvider.System);
        _ = await server.RebuildViews(_api, Token);   // the model has a pending sequence the client never heard of

        Assert.True((await client.Status(_api, Token)).IsSuccess);
    }

    // H3: a ping through the handler is a ping on the model (it is counted).
    [Fact]
    public async Task A_ping_over_http_reaches_the_model()
    {
        var server = new FakeSindriApi();
        var client = new SindriApiClient(new HttpClient(new FakeSindriHandler(server)), new InMemoryHumanToken("tok-1"), TimeProvider.System);

        _ = await client.Ping(_api, Token);

        Assert.Equal(1, server.PingCalls);
    }

    // F2: a view query that never got an answer didn't use it: the next one carries it.
    [Fact]
    public async Task A_query_that_got_no_answer_leaves_the_sequence_for_the_next_one()
    {
        var server = new FakeSindriApi();
        var handler = new FakeSindriHandler(server);
        var client = new SindriApiClient(new HttpClient(handler), new InMemoryHumanToken("tok-1"), TimeProvider.System);
        _ = await client.RebuildViews(_api, Token);

        server.GoDown();
        _ = await client.Status(_api, Token);
        server.ComeUp();
        _ = await client.Status(_api, Token);

        Assert.Equal(["43", "43"], handler.Requests.Skip(1).Select(r => r.After), StringComparer.Ordinal);
    }

    // F2: the retry after a stale token carries it too.
    [Fact]
    public async Task The_stale_token_retry_carries_the_sequence()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi()) { RequiredToken = "tok-2" };
        var tokens = new InMemoryHumanToken("tok-2");
        var client = new SindriApiClient(new HttpClient(handler), tokens, TimeProvider.System);
        _ = await client.RebuildViews(_api, Token);
        tokens.Then("tok-1", "tok-2");

        var status = await client.Status(_api, Token);

        Assert.True(status.IsSuccess);
        Assert.Equal(["43", "43"], handler.Requests.Skip(1).Select(r => r.After), StringComparer.Ordinal);
    }

    // F2: the server waits up to 2 s for the sequence before it answers SND-API-011; the call carrying it outlasts that
    // wait, and like every call it is unreachable only at 120 s.
    [Fact]
    public async Task The_call_carrying_the_sequence_outlasts_the_servers_two_second_wait()
    {
        var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(A.Noon);
        var gate = new GatedHandler();
        var client = new SindriApiClient(new HttpClient(gate), new InMemoryHumanToken("tok-1"), clock);
        _ = await client.RebuildViews(_api, Token);

        var plain = client.Commands(_api, Token);
        clock.Advance(TimeSpan.FromSeconds(120));
        Assert.Equal("SND-UI-002", (await plain).Error.Code);

        var query = client.Services(_api, Token);
        clock.Advance(TimeSpan.FromSeconds(2.5));
        Assert.False(query.IsCompleted, "still waiting at 2.5 s");
        gate.Release();
        Assert.True((await query).IsSuccess);
    }

    // G1: SND-API-011 ("views behind") on such a read is Sindri's code as served, and it used the sequence up: read-your-writes
    // is best effort, so the next read goes without it and shows what Sindri says (a stalled projection never catches up).
    [Fact]
    public async Task Views_behind_is_sindris_code_and_the_sequence_is_used_up()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi());
        var client = new SindriApiClient(new HttpClient(handler), new InMemoryHumanToken("tok-1"), TimeProvider.System);
        _ = await client.RebuildViews(_api, Token);
        handler.Override = (HttpStatusCode.ServiceUnavailable, RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-011", "The views have not reached sequence 43 (fix: retry shortly)").Body);

        var behind = await client.Status(_api, Token);
        handler.Override = null;
        _ = await client.Status(_api, Token);
        _ = await client.Status(_api, Token);

        Assert.Equal(new Error("SND-API-011", ErrorKind.Unavailable, "The views have not reached sequence 43 (fix: retry shortly)"), behind.Error);
        Assert.Equal(["43", null, null], handler.Requests.Skip(1).Select(r => r.After), StringComparer.Ordinal);
    }

    // G1(d): a new process or epoch is another ledger; a sequence from the old one is dropped when ping shows it.
    [Fact]
    public async Task A_ping_from_a_new_epoch_drops_the_pending_sequence()
    {
        var server = new FakeSindriApi();
        var handler = new FakeSindriHandler(server);
        var client = new SindriApiClient(new HttpClient(handler), new InMemoryHumanToken("tok-1"), TimeProvider.System);
        _ = await client.Ping(_api, Token);
        _ = await client.RebuildViews(_api, Token);
        _ = await client.Ping(_api, Token);
        server.Pong = new PingView(4242, 8);

        _ = await client.Ping(_api, Token);
        _ = await client.Status(_api, Token);

        Assert.Null(handler.Requests[^1].After);
    }

    // The same process and epoch (a periodic ping) leaves it for the next view query.
    [Fact]
    public async Task A_ping_from_the_same_epoch_keeps_the_pending_sequence()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi());
        var client = new SindriApiClient(new HttpClient(handler), new InMemoryHumanToken("tok-1"), TimeProvider.System);
        _ = await client.Ping(_api, Token);
        _ = await client.RebuildViews(_api, Token);

        _ = await client.Ping(_api, Token);
        _ = await client.Status(_api, Token);

        Assert.Equal("43", handler.Requests[^1].After);
    }

    // Answers a view query only when released; an ordinary call it never answers (the deadline decides).
    private sealed class GatedHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void Release() => _release.SetResult();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/sindri/views/rebuild")
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(RouteAnswer.SpecShaped("views-rebuild", "accepted").Body) };
            }

            await _release.Task.WaitAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(RouteAnswer.SpecShaped("services", "fixture").Body) };
        }
    }

    // Every context call says human from the UI like the rest: never X-Sindri-Terminal (the protected verbs stay at the CLI), never Origin.
    [Fact]
    public async Task Context_calls_send_human_ui_and_never_terminal_or_origin()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi());
        var client = new SindriApiClient(new HttpClient(handler), new InMemoryHumanToken("tok-1"), TimeProvider.System);

        _ = await client.Lenses(_api, new LensesQuery(["feature:x"], Depth: null, Draft: false, MapVersion: null), Token);
        _ = await client.FileMap(_api, new FileMapQuery("r", Path: null, UnmappedOnly: false, Draft: false), Token);
        _ = await client.ReviewQueue(_api, limit: null, Token);
        _ = await client.ReviewDecision(_api, new ReviewDecisionRequest(DeskVerb.Accept, Node: "code:x"), Token);
        _ = await client.ReviewHistory(_api, limit: null, Token);
        _ = await client.MapStatus(_api, anchor: null, Token);
        _ = await client.Node(_api, "feature:x", Token);
        _ = await client.Sources(_api, Token);

        Assert.Equal(8, handler.Requests.Count);
        Assert.All(handler.Requests, r => Assert.Equal(("human", "ui", null, null, "tok-1"), (r.Caller, r.Client, r.Terminal, r.Origin, r.Token)));
    }
}
