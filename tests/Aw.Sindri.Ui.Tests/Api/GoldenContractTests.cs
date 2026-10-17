using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>
/// UI spec E-17: every API golden reads through the app's own JSON context, so a wire change in Sindri fails this unit
/// lane, not a prover run. A golden is a record the app reads, a ProblemDetails refusal, or a route the app never calls.
/// The goldens are Sindri's own (tests/Aw.Sindri.Tests/Golden/Api, linked into this test output as Golden/Api), plus
/// U1's spec-shaped stand-ins for P2's routes (Golden/SpecShaped) until P2 writes them.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class GoldenContractTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    // The routes the app reads, by slug, and how; a new slug fails until it is placed here or in _notRead.
    private static readonly Dictionary<string, Func<byte[], Result<object>>> _read = new(StringComparer.Ordinal)
    {
        ["ping"] = As<PingView>,
        ["status"] = As<StatusView>,
        ["events"] = AsEvents,
        ["commands"] = As<CommandList>,
        ["services"] = As<ServiceList>,
        ["views-rebuild"] = As<CommandAcceptedView>,
        ["queue"] = As<QueueView>,
        ["items-id"] = As<ItemView>,
        ["items-id-timeline"] = As<TimelineView>,
        ["items"] = As<ItemCreatedView>,   // POST /items' created* goldens only
        ["items-id-close"] = As<CommandAcceptedView>,
        ["workflow-instances"] = As<InstanceList>,
        ["workflow-instance"] = As<InstanceDetail>,
        ["workflow-instance-fact"] = As<FactView>,
        ["workflow-status"] = As<StatusFields>,
        ["workflow-recipe"] = As<RecipeView>,
        ["needs-you"] = As<NeedsYouList>,
        ["decisions"] = As<DecisionList>,
        ["decisions-id"] = As<CommandAcceptedView>,   // POST /decisions/{id}'s accepted goldens; the refusals are ProblemDetails
        ["requests"] = AsRequests,
    };

    // Routes the app never calls yet: stop is Health's (U4), profile approve is protected (the app shows the CLI, §6.5), the profile route is a later page's (U2+), and the
    // context routes are U9's (the context lens), read once U9 lands, until U9 (context records).
    private static readonly HashSet<string> _notRead = new(StringComparer.Ordinal)
    {
        "stop", "profile-approve", "profile",
        "needs-you", "decisions", "decisions-id", "requests",   // API P4: the app reads them in U3
        "context-eval", "context-filemap", "context-lenses", "context-map", "context-map-promote", "context-map-proposals", "context-map-review", "context-map-review-history", "context-node", "context-query",   // until U9 (context records)
        "items",   // GET /items (the list) is never called; POST /items' created* goldens are read (see the loop)
        "ledger",   // API P5: the audit read is the CLI's; the app follows the stream and re-queries
        "services-name-run", "services-name-pause", "services-name-resume", "jobs-id", "jobs-id-cancel", "jobs",   // API P6: the app reads them in U4 (Services and the job panel)
        "items-id-open", "launches-id", "sessions-id-heartbeat", "sessions-id-exit", "launch-targets", "launch-targets-probe",   // API P10a (U8): the app reads the open's answer and the launch in U6a (Open in the terminal); U10: the session wrap's own calls, never the app's
        "events-catalog",   // API P5 (E-15): the app reads each type's invalidates once it switches from re-querying every view on each event (UI lane)
    };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void Every_golden_reads_through_the_apps_json_context_or_is_a_refusal()
    {
        var goldens = GoldenFiles.All().ToList();
        Assert.Contains(goldens, g => g.Kind == "Api" && g.Slug == "status");
        Assert.Contains(goldens, g => g.Kind == "SpecShaped");

        foreach (var (kind, slug, scenario, body) in goldens)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            if (Problem(bytes) is { } problem)
            {
                Assert.StartsWith("SND-", problem.Title, StringComparison.Ordinal);
                Assert.False(string.IsNullOrEmpty(problem.Detail), $"{kind}/{slug}/{scenario}: a refusal without a detail");
                continue;
            }

            if (_notRead.Contains(slug) && !(slug == "items" && scenario.StartsWith("created", StringComparison.Ordinal)))
            {
                continue;
            }

            Assert.True(_read.TryGetValue(slug, out var read), $"{kind}/{slug}/{scenario}: a route the app doesn't place (read it or list it as not read)");
            var value = read(bytes);
            Assert.True(value.IsSuccess, $"{kind}/{slug}/{scenario}: {(value.IsError ? value.Error.Message : string.Empty)}");
        }
    }

    [Fact]
    public void Requests_slug_reads_list_scenarios_as_RequestList_and_others_as_RequestFiled()
    {
        var list = AwJson.Deserialize<RequestList>(Encoding.UTF8.GetBytes(GoldenFiles.Api("requests", "list-fixture")), SindriApiClient.Json).Value;
        var filed = AwJson.Deserialize<RequestFiledView>(Encoding.UTF8.GetBytes(GoldenFiles.Api("requests", "filed")), SindriApiClient.Json).Value;

        var servedFiled = GoldenFiles.Node("requests", "filed");
        Assert.Equal(GoldenFiles.Node("requests", "list-fixture")["requests"]!.AsArray().Select(r => (string)r!["state"]!), list.Requests.Select(r => r.State), StringComparer.Ordinal);
        Assert.Equal(((long)servedFiled["accepted"]!["sequence"]!, (string)servedFiled["requestId"]!), (filed.Accepted.Sequence, filed.RequestId));
        Assert.True(AsRequests(Encoding.UTF8.GetBytes(GoldenFiles.Api("requests", "list-empty"))).Value is RequestList);
        Assert.True(AsRequests(Encoding.UTF8.GetBytes(GoldenFiles.Api("requests", "filed"))).Value is RequestFiledView);
    }

    [Fact]
    public void The_status_goldens_carry_every_field_the_health_page_reads()
    {
        var approved = Read<StatusView>("status", "running-approved");
        var unapproved = Read<StatusView>("status", "running-unapproved");

        Assert.Equal((true, 4242, 1L, 1), (approved.Running, approved.Pid, approved.Epoch, approved.ApiVersion));
        _ = Assert.NotNull(approved.LeaseExpiresAt);
        Assert.Equal((false, false, approved.Profile.Approved), (approved.Profile.NeedsApproval, approved.Profile.ChangedSinceApproval, approved.Profile.Live));
        Assert.EndsWith("profile.yaml", approved.Profile.Source, StringComparison.Ordinal);
        Assert.Equal(("heartbeat", "5s"), (approved.Services[0].Name, approved.Services[0].Every));
        Assert.Equal((null, true), (unapproved.Profile.Approved, unapproved.Profile.NeedsApproval));
        Assert.Empty(unapproved.Services);
    }

    [Fact]
    public void The_ping_and_event_goldens_carry_the_fields_the_follower_reads()
    {
        var ping = Read<PingView>("ping", "running");
        var started = Read<EventView>("events", "process-started");

        Assert.Equal((4242, 1L), (ping.Pid, ping.Epoch));
        Assert.Equal((1L, "process.started"), (started.Sequence, started.Type));
    }

    // FakeSindriHandler writes the 2xx bodies by hand so it can serve any model state; it must write Sindri's shape.
    [Fact]
    public void The_fake_handler_writes_the_goldens_status_ping_and_event_shapes()
    {
        foreach (var scenario in (string[])["running-approved", "running-unapproved"])
        {
            var golden = GoldenFiles.Api("status", scenario);
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(golden), JsonNode.Parse(FakeSindriHandler.Status(Read<StatusView>("status", scenario)))), scenario);
        }

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(GoldenFiles.Api("ping", "running")), JsonNode.Parse(FakeSindriHandler.Ping(Read<PingView>("ping", "running")))));
        var goldenEvent = JsonNode.Parse(GoldenFiles.Api("events", "process-started"))!.AsObject(); // why: the golden is a JSON object
        var fakeEvent = JsonNode.Parse(FakeSindriHandler.Event(Read<EventView>("events", "process-started")))!.AsObject(); // why: as above
        Assert.Equal(goldenEvent.Select(p => p.Key), fakeEvent.Select(p => p.Key), StringComparer.Ordinal);
    }

    // F13 (test-1): the four spec-shaped status goldens (P2's, until P2 writes its own) say what the pill and the follower read.
    [Theory]
    [InlineData("running-healthy", "healthy", 41L, 41L, false, false, null, null, 0)]
    [InlineData("running-degraded", "degraded", 41L, 41L, false, false, null, null, 1)]
    [InlineData("running-rebuilding", "rebuilding", 12L, 42L, true, false, null, null, 0)]
    [InlineData("running-stalled", "stalled", 41L, 42L, false, true, 42L, "service.ran", 1)]
    public void The_spec_shaped_status_goldens_are_read_in_full(string scenario, string word, long cursor, long head, bool rebuilding, bool stalled, long? stalledAt, string? stalledType, int problems)
    {
        var status = AwJson.Deserialize<StatusView>(Encoding.UTF8.GetBytes(GoldenFiles.SpecShaped("status", scenario)), SindriApiClient.Json).Value;

        Assert.Equal(1, status.ApiVersion);
        Assert.Equal((word, problems), (status.Health!.Word, status.Health.Problems.Count));
        Assert.Equal((cursor, head, rebuilding, stalled, stalledAt, stalledType), (status.Views!.Cursor, status.Views.LedgerHead, status.Views.Rebuilding, status.Views.Stalled, status.Views.StalledAt, status.Views.StalledType));
        _ = Assert.NotNull(status.Views.ProjectedAt);
    }

    // F13: the handler writes the spec-shaped status goldens exactly, stalledAt and stalledType included.
    [Theory]
    [InlineData("running-healthy")]
    [InlineData("running-degraded")]
    [InlineData("running-rebuilding")]
    [InlineData("running-stalled")]
    public void The_fake_handler_writes_the_spec_shaped_status_goldens(string scenario)
    {
        var golden = GoldenFiles.SpecShaped("status", scenario);
        var status = AwJson.Deserialize<StatusView>(Encoding.UTF8.GetBytes(golden), SindriApiClient.Json).Value;

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(golden), JsonNode.Parse(FakeSindriHandler.Status(status))), scenario);
    }

    // A stale token: Sindri's human-stale-token golden as served, the token read again and the call retried once.
    [Fact]
    public async Task The_stale_token_golden_is_retried_once_with_the_token_read_again()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi()) { RequiredToken = "fresh-start-token" };
        var token = new InMemoryHumanToken();
        token.Then("stale-start-token", "fresh-start-token");

        var status = await new SindriApiClient(new HttpClient(handler), token, TimeProvider.System).Status(_api, Token);

        Assert.True(status.IsSuccess);
        Assert.Equal(2, handler.Requests.Count);
    }

    // Refused after the retry: the golden's code and its detail as served (the sentence is the golden's, never a copy:
    // SND-AUTH-001's text changes in API P2 unit F), with no third call.
    [Theory]
    [InlineData("stale-start-token", "human-stale-token")]
    [InlineData(null, "human-no-token")]
    public async Task A_refusal_golden_after_the_retry_is_its_code_and_detail(string? held, string scenario)
    {
        var handler = new FakeSindriHandler(new FakeSindriApi()) { RequiredToken = "fresh-start-token" };
        var golden = Read<ProblemView>("status", scenario);

        var status = await new SindriApiClient(new HttpClient(handler), new InMemoryHumanToken(held), TimeProvider.System).Status(_api, Token);

        Assert.Equal(("SND-AUTH-001", ErrorKind.Forbidden, golden.Detail), (status.Error.Code, status.Error.Kind, status.Error.Message));
        Assert.Equal(["/sindri/status", "/sindri/status"], handler.Requests.Select(r => r.Path), StringComparer.Ordinal);
    }

    [Fact]
    public void The_p1_catalog_carries_every_field_the_control_renderer_reads()
    {
        var catalog = AwJson.Deserialize<CommandList>(Encoding.UTF8.GetBytes(GoldenFiles.Api("commands", "catalog")), SindriApiClient.Json).Value;

        var rebuild = Assert.Single(catalog.Rows, r => r.Action == "RebuildViews");
        Assert.Equal(("desk", "ready", "sindri views rebuild", false, false), (rebuild.Class, rebuild.Ui, rebuild.Cli, rebuild.Destructive, rebuild.ReadsViews));
        var stop = Assert.Single(catalog.Rows, r => r.Action == "StopProcess");
        Assert.Equal(("ready", true), (stop.Ui, stop.Destructive));
        Assert.Equal("cli-only:typed-confirmation", Assert.Single(catalog.Rows, r => r.Action == "ApproveProfile").Ui);
    }

    // A refusal's ProblemDetails names its SND code in title; any other body has none.
    private static T Read<T>(string slug, string scenario)
        where T : class =>
        AwJson.Deserialize<T>(Encoding.UTF8.GetBytes(GoldenFiles.Api(slug, scenario)), SindriApiClient.Json).Value;

    // A view's own "title" (an instance's, a child's) is no refusal: ProblemDetails also carries a numeric status.
    private static ProblemView? Problem(byte[] body)
    {
        if (JsonNode.Parse(body) is not JsonObject { } json || json["status"] is not JsonValue { } || json["traceId"] is null)
        {
            return null;
        }

        var problem = AwJson.Deserialize<ProblemView>(body, SindriApiClient.Json);
        return problem is { IsSuccess: true, Value.Title.Length: > 0 } ? problem.Value : null;
    }

    /// <summary>An event golden: one event (S1's), or the data of each frame a stream scenario sent, as an array (API P5).</summary>
    // One slug for GET and POST /requests (U3-API-5): the list-* scenarios are RequestList, the others RequestFiled.
    private static Result<object> AsRequests(byte[] body) =>
        JsonNode.Parse(body) is JsonObject node && node.ContainsKey("requests") ? As<RequestList>(body) : As<RequestFiledView>(body);

    private static Result<object> AsEvents(byte[] body)
    {
        var node = JsonNode.Parse(body);
        if (node is not JsonArray frames)
        {
            return As<EventView>(body);
        }

        foreach (var frame in frames)
        {
            var read = As<EventView>(Encoding.UTF8.GetBytes(frame?.ToJsonString() ?? "null"));
            if (read.IsError)
            {
                return read;
            }
        }

        return frames;
    }

    private static Result<object> As<T>(byte[] body)
        where T : class
    {
        var value = AwJson.Deserialize<T>(body, SindriApiClient.Json);
        return value.IsSuccess ? value.Value : value.Error;
    }
}
