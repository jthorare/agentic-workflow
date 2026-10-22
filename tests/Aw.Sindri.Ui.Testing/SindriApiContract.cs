using System.Net;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// The contract every <see cref="ISindriApi"/> passes: the in-memory fake and the real HTTP client over the canned
/// handler. A binding derives from it and says how to reach a <see cref="FakeSindriApi"/> server.
/// </summary>
public abstract partial class SindriApiContract
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private protected abstract ISindriApi Reach(FakeSindriApi server);

    [Fact]
    public async Task Ping_answers_the_pid_and_epoch()
    {
        var server = new FakeSindriApi { Pong = new PingView(99, 3) };

        var ping = await Reach(server).Ping(_api, Token);

        Expect.Equal(new PingView(99, 3), ping.Value);
    }

    [Fact]
    public async Task A_failing_ping_is_unreachable_while_the_status_still_answers()
    {
        var server = new FakeSindriApi { FailPing = true };
        var api = Reach(server);

        Expect.Equal("SND-UI-002", (await api.Ping(_api, Token)).Error.Code);
        Expect.Equal(4242, (await api.Status(_api, Token)).Value.Pid);
    }

    [Fact]
    public async Task Status_answers_the_lease_profile_and_services()
    {
        var server = new FakeSindriApi { Current = A.Status(pid: 5, epoch: 2, A.ChangedProfile(), A.Heartbeat(A.Noon, "7s")) };

        var status = (await Reach(server).Status(_api, Token)).Value;

        Expect.Equal((5, 2L), (status.Pid, status.Epoch));
        Expect.Equal(A.ChangedProfile(), status.Profile);
        Expect.Sequence([A.Heartbeat(A.Noon, "7s")], status.Services);
    }

    // API spec "Versioning": P1 adds apiVersion to the status; S1's status has none, which the app reads as no mismatch.
    [Fact]
    public async Task Status_carries_the_api_version_when_sindri_reports_one()
    {
        var server = new FakeSindriApi { Current = A.Status() with { ApiVersion = 2 } };
        var api = Reach(server);

        Expect.Equal(2, (await api.Status(_api, Token)).Value.ApiVersion);

        server.Current = A.Status();
        Expect.Equal(expected: null, (await api.Status(_api, Token)).Value.ApiVersion);
    }

    [Fact]
    public async Task Events_after_a_cursor_come_in_order_and_end_with_the_connection()
    {
        var server = new FakeSindriApi();
        server.Push(A.Event(1), A.Event(2), A.Event(3, "profile.approved"));
        var seen = new List<EventView>();

        await foreach (var e in Reach(server).Events(_api, after: 1, Token))
        {
            seen.Add(e);
            if (seen.Count == 2)
            {
                server.Drop();
            }
        }

        Expect.Sequence([A.Event(2), A.Event(3, "profile.approved")], seen);
    }

    // API §6 and §3.6: a refused call (403 ProblemDetails, e.g. SND-AUTH-001 for a missing or stale token) is Sindri's
    // code and detail as they are, a Forbidden error; never SND-UI-003, which is for answers this build can't read.
    [Fact]
    public async Task A_refused_call_is_sindris_code_and_detail_as_they_are()
    {
        var server = new FakeSindriApi { Refusal = A.NoTokenRefusal };
        var api = Reach(server);

        var ping = await api.Ping(_api, Token);
        var status = await api.Status(_api, Token);

        Expect.Equal(new Error(A.NoTokenRefusal.Code, ErrorKind.Forbidden, A.NoTokenRefusal.Detail), ping.Error);
        Expect.Equal(new Error(A.NoTokenRefusal.Code, ErrorKind.Forbidden, A.NoTokenRefusal.Detail), status.Error);
        Expect.Sequence([], await api.Events(_api, 0, Token).ToListAsync(Token));
    }

    [Fact]
    public async Task A_server_that_is_down_is_unreachable()
    {
        var server = new FakeSindriApi();
        server.GoDown();
        var api = Reach(server);

        var ping = await api.Ping(_api, Token);
        var status = await api.Status(_api, Token);

        Expect.Equal("SND-UI-002", ping.Error.Code);
        Expect.Equal("SND-UI-002", status.Error.Code);
        Expect.Sequence([], await api.Events(_api, 0, Token).ToListAsync(Token));
    }

    // GET /sindri/commands (API P1): the catalog as Sindri's golden serves it.
    [Fact]
    public async Task Commands_answer_the_catalog_rows_with_their_ui_status()
    {
        var catalog = (await Reach(new FakeSindriApi()).Commands(_api, Token)).Value;

        Expect.Equal(71, catalog.Rows.Count);
        Expect.Equal("ready", catalog.Rows.Single(r => r.Action == "RebuildViews").Ui);
        Expect.Equal("sindri profile approve [<hash>]", catalog.Rows.Single(r => r.Action == "ApproveProfile").Cli);
    }

    // GET /sindri/services (API P2, spec-shaped): U1 reads the view's EmptyText sentence and its asOf.
    [Fact]
    public async Task Services_answer_their_empty_sentence_and_freshness()
    {
        var server = new FakeSindriApi { ServiceView = RouteAnswer.SpecShaped("services", "stalled") };

        var view = (await Reach(server).Services(_api, Token)).Value;

        Expect.Equal("Views are stalled at 42: run `sindri views rebuild`", view.Empty);
        Expect.Equal((41L, 42L, true, false), (view.AsOf.Cursor, view.AsOf.LedgerHead, view.AsOf.Stalled, view.AsOf.Rebuilding));
    }

    // POST /sindri/views/rebuild (API P2, spec-shaped): accepted with the sequence the next query waits for.
    [Fact]
    public async Task Rebuild_views_answers_the_accepted_sequence()
    {
        var accepted = (await Reach(new FakeSindriApi()).RebuildViews(_api, Token)).Value;

        Expect.Equal(43L, accepted.Sequence);
    }

    // UI spec §4.1 Error: a refusal is the API's title (the SND code) and detail as served, never the app's words.
    [Fact]
    public async Task A_refused_command_is_the_apis_code_and_detail()
    {
        var server = new FakeSindriApi { Rebuild = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-009", "Sindri is busy with another command (fix: retry)") };

        var refused = await Reach(server).RebuildViews(_api, Token);

        Expect.Equal(("SND-API-009", "Sindri is busy with another command (fix: retry)"), (refused.Error.Code, refused.Error.Message));
    }

    // F12 (test-0): every route answers a refusal, a stopped server and an odd body the same way, whichever API is behind it.
    [Fact]
    public async Task A_403_refusal_reaches_every_route_as_sindris_code_and_detail()
    {
        var api = Reach(new FakeSindriApi { Refusal = A.NoTokenRefusal });
        var expected = new Error(A.NoTokenRefusal.Code, ErrorKind.Forbidden, A.NoTokenRefusal.Detail);

        Expect.Equal(expected, (await api.Commands(_api, Token)).Error);
        Expect.Equal(expected, (await api.Services(_api, Token)).Error);
        Expect.Equal(expected, (await api.RebuildViews(_api, Token)).Error);
        Expect.Sequence(Enumerable.Repeat(expected, 5), await P3Errors(api));   // L13: the P3 routes too
    }

    // J5 (arch-1): the order a real server answers in: nothing answers (Down), then the caller check (a refusal), then the route's own
    // failures. So a refusal outranks a failing status or ping, and a stopped server outranks a refusal.
    [Fact]
    public async Task A_refusal_outranks_the_routes_own_failures_and_a_stopped_server_outranks_a_refusal()
    {
        var server = new FakeSindriApi { Refusal = A.NoTokenRefusal, FailStatus = true, FailPing = true };
        var api = Reach(server);
        var expected = new Error(A.NoTokenRefusal.Code, ErrorKind.Forbidden, A.NoTokenRefusal.Detail);

        Expect.Equal(expected, (await api.Ping(_api, Token)).Error);
        Expect.Equal(expected, (await api.Status(_api, Token)).Error);

        server.GoDown();

        Expect.Equal("SND-UI-002", (await api.Ping(_api, Token)).Error.Code);
        Expect.Equal("SND-UI-002", (await api.Status(_api, Token)).Error.Code);
        Expect.Equal("SND-UI-002", (await api.Services(_api, Token)).Error.Code);
    }

    [Fact]
    public async Task A_stopped_server_is_unreachable_on_every_route()
    {
        var server = new FakeSindriApi();
        server.GoDown();
        var api = Reach(server);

        Expect.Equal("SND-UI-002", (await api.Commands(_api, Token)).Error.Code);
        Expect.Equal("SND-UI-002", (await api.Services(_api, Token)).Error.Code);
        Expect.Equal("SND-UI-002", (await api.RebuildViews(_api, Token)).Error.Code);
        Expect.Sequence(Enumerable.Repeat("SND-UI-002", 5), (await P3Errors(api)).Select(e => e.Code));
    }

    [Theory]
    [InlineData(409, ErrorKind.Conflict)]
    [InlineData(429, ErrorKind.RateLimited)]
    [InlineData(504, ErrorKind.Timeout)]
    [InlineData(503, ErrorKind.Unavailable)]
    [InlineData(404, ErrorKind.NotFound)]
    public async Task A_refusal_keeps_its_code_detail_and_kind_on_every_route(int status, ErrorKind kind)
    {
        var problem = RouteAnswer.Problem((HttpStatusCode)status, "SND-API-777", "Not now (fix: later)");
        var api = Reach(new FakeSindriApi
        {
            Catalog = problem,
            ServiceView = problem,
            Rebuild = problem,
            QueueView = problem,
            Items = { [A.NewItem] = problem },
            TimelinePages = { [0] = problem },
            Create = problem,
            Close = problem,
        });
        var expected = new Error("SND-API-777", kind, "Not now (fix: later)");

        Expect.Equal(expected, (await api.Commands(_api, Token)).Error);
        Expect.Equal(expected, (await api.Services(_api, Token)).Error);
        Expect.Equal(expected, (await api.RebuildViews(_api, Token)).Error);
        Expect.Sequence(Enumerable.Repeat(expected, 5), await P3Errors(api));
    }

    // A body that isn't Sindri's ProblemDetails (not an SND title, no detail, not JSON) is an answer the app can't read.
    [Theory]
    [InlineData(500, "boom")]
    [InlineData(418, "{\"title\":\"Teapot\",\"detail\":\"short and stout\"}")]
    [InlineData(500, "{\"title\":\"SND-API-001\"}")]
    public async Task A_body_that_is_not_sindris_is_an_unreadable_answer_on_every_route(int status, string body)
    {
        var odd = new RouteAnswer((HttpStatusCode)status, body);
        var api = Reach(new FakeSindriApi
        {
            Catalog = odd,
            ServiceView = odd,
            Rebuild = odd,
            QueueView = odd,
            Items = { [A.NewItem] = odd },
            TimelinePages = { [0] = odd },
            Create = odd,
            Close = odd,
        });

        Expect.Equal("SND-UI-003", (await api.Commands(_api, Token)).Error.Code);
        Expect.Equal("SND-UI-003", (await api.Services(_api, Token)).Error.Code);
        Expect.Equal("SND-UI-003", (await api.RebuildViews(_api, Token)).Error.Code);
        Expect.Sequence(Enumerable.Repeat("SND-UI-003", 5), (await P3Errors(api)).Select(e => e.Code));
    }

    // H3 (arch-0, test-2): read-your-writes (API §2.4) under both bindings. The command's sequence is used up by the next
    // view read, whatever it answers: SND-API-011 when the views lag, then a read that shows what Sindri says.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_status_read_after_an_accepted_command_is_views_behind_once_when_the_views_lag(bool stalled)
    {
        var api = Reach(new FakeSindriApi { Lag = stalled ? ViewsLag.Stalled : ViewsLag.Behind });
        _ = await api.RebuildViews(_api, Token);

        var first = await api.Status(_api, Token);

        Expect.Equal(("SND-API-011", ErrorKind.Unavailable), (first.Error.Code, first.Error.Kind));
        Expect.Equal(4242, (await api.Status(_api, Token)).Value.Pid);
    }

    [Fact]
    public async Task A_services_read_after_an_accepted_command_is_views_behind_once_when_the_views_lag()
    {
        var api = Reach(new FakeSindriApi { Lag = ViewsLag.Behind });
        _ = await api.RebuildViews(_api, Token);

        var first = await api.Services(_api, Token);

        Expect.Equal(("SND-API-011", ErrorKind.Unavailable), (first.Error.Code, first.Error.Kind));
        Expect.True((await api.Services(_api, Token)).IsSuccess);
    }

    // With no lag the read is answered at once, with the sequence or without.
    [Fact]
    public async Task A_read_after_an_accepted_command_is_answered_when_the_views_have_caught_up()
    {
        var api = Reach(new FakeSindriApi());
        _ = await api.RebuildViews(_api, Token);

        Expect.Equal(4242, (await api.Status(_api, Token)).Value.Pid);
    }

    // Another process or epoch is another ledger: the sequence from the old one is dropped when a ping shows the change.
    [Fact]
    public async Task A_ping_that_shows_a_new_epoch_drops_the_pending_sequence()
    {
        var server = new FakeSindriApi { Lag = ViewsLag.Behind };
        var api = Reach(server);
        _ = await api.Ping(_api, Token);
        _ = await api.RebuildViews(_api, Token);
        server.Pong = new PingView(4242, 8);
        _ = await api.Ping(_api, Token);

        Expect.Equal(4242, (await api.Status(_api, Token)).Value.Pid);
    }

    // A read that never got an answer (a transport failure) didn't use the sequence: the next one carries it.
    [Fact]
    public async Task A_status_read_that_never_got_an_answer_leaves_the_sequence_for_the_next_one()
    {
        var server = new FakeSindriApi { Lag = ViewsLag.Behind };
        var api = Reach(server);
        _ = await api.RebuildViews(_api, Token);
        server.FailStatus = true;

        Expect.Equal("SND-UI-002", (await api.Status(_api, Token)).Error.Code);
        server.FailStatus = false;

        Expect.Equal("SND-API-011", (await api.Status(_api, Token)).Error.Code);
    }

    // GET /sindri/queue (API P3, spec-shaped): every group in the API's order, empty ones included with their own sentence (E-18).
    [Fact]
    public async Task Queue_answers_every_group_in_order_with_its_own_empty_sentence()
    {
        var queue = (await Reach(new FakeSindriApi()).Queue(_api, Token)).Value;

        Expect.Sequence(["waitingOnYou", "withYou", "running", "queued"], queue.Groups.Select(g => g.Group));
        Expect.Sequence([0, 0, 0, 4], queue.Groups.Select(g => g.Items.Count));
        Expect.Equal("Nothing is waiting on you.", queue.Groups[0].Empty);
        Expect.Equal("Nothing running.", queue.Groups[2].Empty);
        Expect.True(queue.Groups.SelectMany(g => g.Items).All(i => i.PrimaryAction is null && i.Owner is null));
        Expect.Equal(expected: null, queue.Groups[3].Empty);
        Expect.Equal(("new", "Try the queue"), (queue.Groups[3].Items[3].State, queue.Groups[3].Items[3].Title));
    }

    // An empty queue: the view's own sentence, and each group's.
    [Fact]
    public async Task An_empty_queue_answers_the_views_sentence()
    {
        var queue = (await Reach(new FakeSindriApi { QueueView = RouteAnswer.SpecShaped("queue", "empty") }).Queue(_api, Token)).Value;

        Expect.True(queue.Groups.All(g => g.Items.Count == 0 && g.Empty is { Length: > 0 }));
        Expect.Equal("Nothing in the queue. Add one with `sindri item add`.", queue.Empty);
    }

    // GET /sindri/items/{id} (API P3, spec-shaped): the version CloseItem sends back, and the actions allowed now.
    [Fact]
    public async Task An_item_answers_its_version_and_actions()
    {
        var item = (await Reach(new FakeSindriApi()).Item(_api, A.NewItem, Token)).Value;

        Expect.Equal((A.NewItem, 1L, "human:cli"), (item.Id, item.Version, item.CreatedBy));
        Expect.Sequence(["CloseItem:done", "CloseItem:cancelled"], item.Actions);
        Expect.Equal(("Check the queue shows a new item without a click.", "linear:issue/ENG-142"), (item.Body, item.Link));
        Expect.Equal(expected: null, item.Owner);   // README X61: owner is served, null for now; the app builds no owner UI
    }

    // UI spec §4.9 Item detail: an unknown item is the API's SND-ITEM-404 with its detail as served.
    [Fact]
    public async Task An_unknown_item_is_the_apis_SND_ITEM_404()
    {
        var missing = await Reach(new FakeSindriApi()).Item(_api, A.MissingItem, Token);

        Expect.Equal(("SND-ITEM-404", A.Detail("items-id", "not-found")), (missing.Error.Code, missing.Error.Message));
    }

    // GET /sindri/items/{id}/timeline?after= (API P3, spec-shaped): a page and the cursor of the next; the last page has none.
    [Fact]
    public async Task The_timeline_pages_by_its_next_cursor()
    {
        var api = Reach(new FakeSindriApi());

        var first = (await api.Timeline(_api, A.NewItem, after: 0, Token)).Value;
        var second = (await api.Timeline(_api, A.NewItem, after: first.Next!.Value, Token)).Value;

        Expect.Equal(100, first.Entries.Count);
        Expect.Equal(151L, first.Next);
        Expect.Sequence([152L], second.Entries.Select(e => e.Sequence));
        Expect.Equal(expected: null, second.Next);
    }

    // POST /sindri/items (CreateItem, desk, API P3): the body as sent, and the new id with the accepted sequence.
    [Fact]
    public async Task Create_item_sends_the_form_and_answers_the_new_id()
    {
        var server = new FakeSindriApi();

        var created = (await Reach(server).CreateItem(_api, new CreateItemRequest("Proof: appears live", "agentic-workflow", "A body", Link: null), Token)).Value;

        Expect.Equal(("ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a306", 58L), (created.ItemId, created.Accepted.Sequence));
        Expect.Sequence([new CreateItemRequest("Proof: appears live", "agentic-workflow", "A body", Link: null)], server.Created);
    }

    // POST /sindri/items/{id}/close (CloseItem, desk, API P3): the reason and the version the human saw.
    [Fact]
    public async Task Close_item_sends_the_reason_and_the_version_seen()
    {
        var server = new FakeSindriApi();

        var accepted = (await Reach(server).CloseItem(_api, A.NewItem, new CloseItemRequest("cancelled", 1), Token)).Value;

        Expect.Equal(59L, accepted.Sequence);
        Expect.Sequence([(A.NewItem, new CloseItemRequest("cancelled", 1))], server.Closed);
    }

    // UI spec §9.1: a stale version is the API's SND-ITEM-409 with its detail as served.
    [Fact]
    public async Task Close_with_a_stale_version_is_the_apis_SND_ITEM_409()
    {
        var server = new FakeSindriApi { Close = RouteAnswer.Problem("items-id-close", "stale-version") };

        var refused = await Reach(server).CloseItem(_api, A.NewItem, new CloseItemRequest("done", 1), Token);

        Expect.Equal(("SND-ITEM-409", A.Detail("items-id-close", "stale-version")), (refused.Error.Code, refused.Error.Message));
    }
    // LANE ANCHOR U3': contract cases for the U3' reads go in a new partial file (SindriApiContract.<Slice>.cs); only a hook-up line goes here. Insert immediately above this marker, never between the markers.
    // LANE ANCHOR U8: contract cases for the U8 sends go in SindriApiContract.Workflow.cs; only a hook-up line goes here. Insert immediately below this marker, never between the markers.
}
