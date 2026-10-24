using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Telemetry;
using Aw.Kernel.Telemetry.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.ContextCalls;
using Aw.Sindri.Tests.McpGoldens;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Tests.Mcp;

/// <summary>sindri_request (API spec §8, P8 unit D): the one command a session has over MCP. It files a request as 'session:mcp', which a human settles.</summary>
public sealed class SindriRequestToolTests(SharedMcpWorld shared) : IClassFixture<SharedMcpWorld>
{
    // why: the caller's secrets and the request's own words must not be mistaken for each other in a log scan
    private const string Words = "zebra quokka rescope plea";

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Sindri_request_files_a_rescope_that_a_human_sees_in_needs_you()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = shared.World;
        await using var mcp = await McpSession.Open(world.Host, "alpha", ct);

        var result = await mcp.Call("sindri_request", Request("rescope", "Split the work in two.", world.AlphaItem), ct);

        Assert.NotEqual(true, result.IsError);
        var decision = result.StructuredContent!.Value.GetProperty("decisionId").GetString()!;
        var requestId = result.StructuredContent.Value.GetProperty("requestId").GetString()!;
        Assert.StartsWith($"filed {requestId} (", Text(result), StringComparison.Ordinal);
        Assert.Contains($"(rescope) as {decision}; a human settles it", Text(result), StringComparison.Ordinal);
        var needs = await world.Human("/sindri/needs-you", ct);
        var pending = JsonDocument.Parse(await world.Human("/sindri/decisions?pending=true", ct)).RootElement.GetProperty("decisions");
        Assert.Contains(decision, needs, StringComparison.Ordinal);
        var row = pending.EnumerateArray().Single(d => d.GetProperty("id").GetString() == decision);
        Assert.Equal("alpha", row.GetProperty("repo").GetString());
        Assert.Equal("session:mcp", row.GetProperty("filedBy").GetString());
        McpGolden.Match(result, world.Host.Home, "sindri_request", "filed");
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_request_without_itemId_records_the_sessions_repo()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = shared.World;
        await using var mcp = await McpSession.Open(world.Host, "beta", ct);

        var result = await mcp.Call("sindri_request", Request("note", "Nothing is blocking, FYI."), ct);

        Assert.NotEqual(true, result.IsError);
        var decision = result.StructuredContent!.Value.GetProperty("decisionId").GetString()!;
        var pending = JsonDocument.Parse(await world.Human("/sindri/decisions?pending=true", ct)).RootElement.GetProperty("decisions");
        var row = pending.EnumerateArray().Single(d => d.GetProperty("id").GetString() == decision);
        Assert.Equal("beta", row.GetProperty("repo").GetString());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("item").ValueKind);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_request_against_another_repos_item_is_filed_against_that_repo()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await McpWorld.Start(ct);
        await using var mcp = await McpSession.Open(world.Host, "alpha", ct);

        var result = await mcp.Call("sindri_request", Request("rescope", "Not yours.", world.BetaItem), ct);

        Assert.NotEqual(true, result.IsError);
        await world.Settle(ct);
        Assert.Contains("Not yours.", await world.Human("/sindri/ledger?type=request.filed", ct), StringComparison.Ordinal);
        McpGolden.Match(result, world.Host.Home, "sindri_request", "other-repo-item");
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Text_over_the_cap_is_SND_REQ_001()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = shared.World;
        await using var mcp = await McpSession.Open(world.Host, "alpha", ct);

        var result = await mcp.Call("sindri_request", Request("note", new string('x', RequestRules.TextMax + 1)), ct);

        Assert.Equal("SND-REQ-001", Code(result));
        McpGolden.Match(result, world.Host.Home, "sindri_request", "text-too-long");
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task The_201st_open_request_in_a_repo_is_SND_REQ_002()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await McpWorld.Start(ct);
        await using var mcp = await McpSession.Open(world.Host, "alpha", ct);
        for (var n = 1; n <= RequestRules.MaxOpenPerRepo; n++)
        {
            var filed = await mcp.Call("sindri_request", Request("note", string.Create(CultureInfo.InvariantCulture, $"Note {n}")), ct);
            Assert.NotEqual(true, filed.IsError);
        }

        var result = await mcp.Call("sindri_request", Request("note", "One too many."), ct);

        Assert.Equal("SND-REQ-002", Code(result));
        McpGolden.Match(result, world.Host.Home, "sindri_request", "rate-limited");
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_request_is_audited_as_command_handled_by_session_mcp()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await McpWorld.Start(ct);
        await using var mcp = await McpSession.Open(world.Host, "alpha", ct);

        _ = await mcp.Call("sindri_request", Request("note", "Audit me."), ct);
        await world.Settle(ct);

        var audit = await world.Human("/sindri/ledger?type=command.handled", ct);
        Assert.Contains("session:mcp", audit, StringComparison.Ordinal);
        Assert.Contains("FileRequest", audit, StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Pending_decisions_right_after_a_request_includes_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = shared.World;
        await using var mcp = await McpSession.Open(world.Host, "alpha", ct);

        var filed = await mcp.Call("sindri_request", Request("handback", "Take it back.", world.AlphaItem), ct);
        var pending = await mcp.Call("sindri_pending_decisions", "{}", ct);

        var decision = filed.StructuredContent!.Value.GetProperty("decisionId").GetString();
        Assert.Contains(pending.StructuredContent!.Value.GetProperty("decisions").EnumerateArray(), d => d.GetProperty("id").GetString() == decision);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Sindri_request_waits_for_the_views_to_reach_its_own_write_before_it_answers()
    {
        var ct = TestContext.Current.CancellationToken;
        var spy = new WaitSpy();
        await using var world = await McpWorld.Start(ct, pump: false, extra: spy.Register);
        long behind = -1;
        spy.OnLaterWait = async (inner, sequence, limit, token) =>
        {
            behind = inner.Current.Cursor;   // the views are still behind the write when the tool starts to wait
            await ViewScenarios.CatchUp(world.Host, token);
            return await inner.WaitFor(sequence, limit, token);
        };
        await using var mcp = await McpSession.Open(world.Host, "alpha", ct);

        var result = await mcp.Call("sindri_request", Request("note", "Wait for me."), ct);

        Assert.NotEqual(true, result.IsError);
        var sequence = result.StructuredContent!.Value.GetProperty("accepted").GetProperty("sequence").GetInt64();
        Assert.True(behind < sequence, "the views were already past the write, so nothing was waited for");
        Assert.Equal(2, spy.Calls.Count);   // the command's own wait for the head, then the tool's wait for its write
        Assert.Equal((sequence, ReadYourWrites.Limit), spy.Calls[1]);
        Assert.DoesNotContain("catching up", Text(result), StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Views_that_stay_behind_the_write_still_answer_success_with_a_catching_up_note()
    {
        var ct = TestContext.Current.CancellationToken;
        var spy = new WaitSpy();
        await using var world = await McpWorld.Start(ct, pump: false, extra: spy.Register);
        spy.OnLaterWait = (_, sequence, _, _) => ValueTask.FromResult<Result<Unit>>(SindriErrors.ViewsBehind.With(string.Create(CultureInfo.InvariantCulture, $"the views didn't reach {sequence}")));
        await using var mcp = await McpSession.Open(world.Host, "alpha", ct);

        var result = await mcp.Call("sindri_request", Request("note", "The views are slow."), ct);

        Assert.NotEqual(true, result.IsError);
        Assert.Contains("(views catching up: sindri_pending_decisions may not list it yet)", Text(result), StringComparison.Ordinal);
        Assert.Equal(result.StructuredContent!.Value.GetProperty("accepted").GetProperty("sequence").GetInt64(), spy.Calls[^1].Sequence);
        Assert.Equal(ReadYourWrites.Limit, spy.Calls[^1].Limit);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_human_caller_terminal_and_token_header_on_the_mcp_endpoint_still_files_as_session_mcp()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = shared.World;
        await using var mcp = await McpSession.Open(world.Host, "alpha", ct,
            (SindriHeaders.Caller, "human"), (SindriHeaders.Terminal, "1"), (SindriHeaders.Token, world.Host.Token.Value));

        var result = await mcp.Call("sindri_request", Request("note", "I claim to be a person."), ct);
        var decision = result.StructuredContent!.Value.GetProperty("decisionId").GetString()!;
        var pending = JsonDocument.Parse(await world.Human("/sindri/decisions?pending=true", ct)).RootElement.GetProperty("decisions");

        Assert.NotEqual(true, result.IsError);
        Assert.Equal("session:mcp", pending.EnumerateArray().Single(d => d.GetProperty("id").GetString() == decision).GetProperty("filedBy").GetString());
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task An_x_sindri_after_or_launch_secret_header_is_dropped_on_the_mcp_endpoint()
    {
        var ct = TestContext.Current.CancellationToken;
        var seen = new HeaderTap();
        await using var host = await GoldenHost.Start(temporary: true, ct, SeededMcp.Profile, extra: services => services.Replace(ServiceDescriptor.Singleton<IHttpContextAccessor>(seen)));
        await using var mcp = await McpSession.Open(host, "alpha", ct,
            (SindriHeaders.After, "99999"), ("X-Sindri-Launch-Secret", "s3cret"), (SindriHeaders.Caller, "human"), ("X-Sindri-Launch", "L-1"),
            ("x-sindri-caller", "human"), (SindriHeaders.Client, "ui"));

        var result = await mcp.Call("sindri_status", "{}", ct);

        Assert.NotEqual(true, result.IsError);
        Assert.Equal(["X-Sindri-Client", "X-Sindri-Repo"], seen.Names.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal), StringComparer.OrdinalIgnoreCase);
        Assert.Equal("mcp", seen.Client);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Neither_request_text_nor_item_text_reaches_a_log_line_even_at_trace()
    {
        var ct = TestContext.Current.CancellationToken;
        var sink = new InMemoryLogSink();
        var runtime = TelemetryRuntime.Start(new TelemetryOptions { ServiceName = "sindri", Sink = sink, Clock = TimeProvider.System, MinimumLevel = LogLevel.Trace });
        await using var host = await GoldenHost.Start(temporary: true, ct, SeededMcp.Profile, extra: services => services
            .Replace(ServiceDescriptor.Singleton(runtime))
            .Replace(ServiceDescriptor.Singleton(runtime.LoggerFactory)));
        await ViewScenarios.Approve(host, ct);
        var item = await ItemScenarios.Create(host, "Quagga title text", "alpha", body: "Wildebeest body text");
        await ViewScenarios.CatchUp(host, ct);   // why: the request names the item, which the views must hold before the tool reads it
        await using var pump = McpPump.Start(host);
        await using var mcp = await McpSession.Open(host, "alpha", ct);

        var filed = await mcp.Call("sindri_request", Request("rescope", Words, item), ct);
        var read = await mcp.Call("sindri_item", new JsonObject { ["itemId"] = item }.ToJsonString(), ct);
        runtime.Flush();

        Assert.False(filed.IsError ?? false, Text(filed));
        Assert.False(read.IsError ?? false, Text(read));
        Assert.NotEmpty(sink.Lines);
        Assert.DoesNotContain("zebra", sink.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Quagga", sink.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Wildebeest", sink.Text, StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task The_endpoints_own_refusals_are_021_022_and_023()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = shared.World;
        await using var mcp = await McpSession.Open(world.Host, "alpha", ct);
        await using var none = await McpSession.Open(world.Host, repo: null, ct);

        var unknownTool = await mcp.Call("sindri_decide", """{"choice":"accept"}""", ct);
        var noRepo = await none.Call("sindri_request", Request("note", "Hello."), ct);
        var unknownArgument = await mcp.Call("sindri_request", """{"kind":"note","text":"Hi.","priority":1}""", ct);
        var badKind = await mcp.Call("sindri_request", """{"kind":"approve","text":"Hi."}""", ct);

        Assert.Equal(["SND-CONTEXT-023", "SND-CONTEXT-021", "SND-CONTEXT-022", "SND-CONTEXT-022"],
            new[] { unknownTool, noRepo, unknownArgument, badKind }.Select(Code), StringComparer.Ordinal);
        Assert.Contains("rescope, add-task, note, handback", Text(badKind), StringComparison.Ordinal);
        McpGolden.Match(unknownTool, world.Host.Home, "errors", "unknown-tool");
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Blank_text_and_blank_ids_get_the_codes_the_http_routes_give()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = shared.World;
        await using var mcp = await McpSession.Open(world.Host, "alpha", ct);

        var blankText = await mcp.Call("sindri_request", """{"kind":"note","text":"  "}""", ct);
        var emptyText = await mcp.Call("sindri_request", """{"kind":"note","text":""}""", ct);
        var emptyItemOnRequest = await mcp.Call("sindri_request", """{"kind":"note","text":"Hi.","itemId":""}""", ct);
        var blankItem = await mcp.Call("sindri_item", """{"itemId":"  "}""", ct);
        var emptyItemOnDecisions = await mcp.Call("sindri_pending_decisions", """{"itemId":""}""", ct);
        var emptyJob = await mcp.Call("sindri_job", """{"jobId":""}""", ct);

        Assert.Equal(["SND-REQ-001", "SND-REQ-001", "SND-CLI-003", "SND-CLI-003", "SND-CLI-003", "SND-CLI-003"],
            new[] { blankText, emptyText, emptyItemOnRequest, blankItem, emptyItemOnDecisions, emptyJob }.Select(Code), StringComparer.Ordinal);
        var http = await world.Human("/sindri/items/%20", ct);
        Assert.Contains("SND-CLI-003", http, StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_call_without_a_repo_matches_its_golden()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = shared.World;
        await using var none = await McpSession.Open(world.Host, repo: null, ct);

        var result = await none.Call("sindri_request", Request("note", "Hello."), ct);

        McpGolden.Match(result, world.Host.Home, "errors", "no-repo");
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task An_unknown_argument_matches_its_golden()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = shared.World;
        await using var mcp = await McpSession.Open(world.Host, "alpha", ct);

        var result = await mcp.Call("sindri_request", """{"kind":"note","text":"Hi.","priority":1}""", ct);

        McpGolden.Match(result, world.Host.Home, "errors", "unknown-argument");
    }

    private static string Request(string kind, string text, string? item = null) =>
        new JsonObject { ["kind"] = kind, ["text"] = text, ["itemId"] = item }.ToJsonString();

    private static string Text(CallToolResult result) => string.Concat(result.Content.OfType<TextContentBlock>().Select(b => b.Text));

    private static string? Code(CallToolResult result) => result.StructuredContent is { } s && s.TryGetProperty("code", out var code) ? code.GetString() : null;

    /// <summary>The projector's status with every wait recorded; from the second wait on (the first is the command's own, for the ledger head) a test can answer in place of the projector.</summary>
    private sealed class WaitSpy : IProjectorStatus
    {
        private readonly List<(long Sequence, TimeSpan Limit)> _calls = [];

        public Func<IProjectorStatus, long, TimeSpan, CancellationToken, ValueTask<Result<Unit>>>? OnLaterWait { get; set; }

        public IReadOnlyList<(long Sequence, TimeSpan Limit)> Calls
        {
            get
            {
                lock (_calls)
                {
                    return [.. _calls];
                }
            }
        }

        public ViewsStatus Current => Inner.Current;

        private IProjectorStatus Inner { get => field ?? throw new InvalidOperationException("the spy isn't resolved"); set; }

        public void Register(IServiceCollection services) =>
            services.Replace(ServiceDescriptor.Singleton<IProjectorStatus>(sp => { Inner = sp.GetRequiredService<ProjectorProgress>(); return this; }));

        public ValueTask<Result<Unit>> WaitFor(long sequence, TimeSpan limit, CancellationToken cancellationToken)
        {
            int count;
            lock (_calls)
            {
                _calls.Add((sequence, limit));
                count = _calls.Count;
            }

            return count > 1 && OnLaterWait is { } later ? later(Inner, sequence, limit, cancellationToken) : Inner.WaitFor(sequence, limit, cancellationToken);
        }
    }

    /// <summary>Records the X-Sindri-* request headers each time the host reads the current request.</summary>
    private sealed class HeaderTap : IHttpContextAccessor
    {
        private readonly HttpContextAccessor _inner = new();
        private readonly List<string> _names = [];

        public string? Client { get; private set; }

        public IReadOnlyList<string> Names
        {
            get
            {
                lock (_names)
                {
                    return [.. _names];
                }
            }
        }

        public HttpContext? HttpContext
        {
            get
            {
                var context = _inner.HttpContext;
                if (context is not null && context.Request.Path.StartsWithSegments("/sindri/mcp", StringComparison.OrdinalIgnoreCase))
                {
                    lock (_names)
                    {
                        _names.Clear();
                        _names.AddRange(context.Request.Headers.Keys.Where(k => k.StartsWith("X-Sindri-", StringComparison.OrdinalIgnoreCase)));
                    }

                    Client = context.Request.Headers[SindriHeaders.Client].ToString();
                }

                return context;
            }

            set => _inner.HttpContext = value;
        }
    }
}
