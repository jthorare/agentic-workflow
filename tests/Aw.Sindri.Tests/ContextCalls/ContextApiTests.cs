using System.Net;
using System.Text.Json;
using Aw.Context.Testing.Fixtures;
using Aw.Kernel.Telemetry;
using Aw.Kernel.Telemetry.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ContextCalls;

public sealed class ContextApiTests : IDisposable
{
    private readonly FixtureRepo _repo = FixtureRepo.Build();

    public void Dispose() => _repo.Dispose();

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_human_gets_cited_repo_docs_after_the_refresh_service_ran_and_records_its_event()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Approved(sessions: false, ct);

        var answer = await host.Http.Ask(Get("/sindri/context/query?q=orientation%20toggle", Human(host.Token.Value)));
        var sources = await host.Http.Ask(Get("/sindri/context/sources", Human(host.Token.Value)));
        var synced = await host.App.Services.GetRequiredService<ILedgerStore>().Latest(EventTypes.ContextSourcesSynced, ct);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        using var result = JsonDocument.Parse(answer.Body);
        var cite = result.RootElement.GetProperty("cites").GetProperty("C1");
        Assert.StartsWith("repo:split-panes/docs/", cite.GetProperty("key").GetString(), StringComparison.Ordinal);
        Assert.Equal("current", cite.GetProperty("state").GetString());
        Assert.Equal(40, cite.GetProperty("refs")[0].GetProperty("version").GetString()!.Length);   // why: a blob sha
        Assert.Contains("\"state\":\"ok\"", sources.Body, StringComparison.Ordinal);
        Assert.NotNull(synced.Value);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Sessions_are_gated_scoped_and_a_foreign_origin_is_refused()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var off = await Approved(sessions: false, ct);
        var gated = await off.Http.Ask(Get("/sindri/context/query?q=orientation", (SindriHeaders.Caller, "session"), (SindriHeaders.Repo, "split-panes")));
        var origin = await off.Http.Ask(Get("/sindri/context/query?q=orientation", [.. Human(off.Token.Value), ("Origin", "https://evil.example")]));
        await using var on = await Approved(sessions: true, ct);
        var own = await on.Http.Ask(Get("/sindri/context/query?q=orientation", (SindriHeaders.Caller, "session"), (SindriHeaders.Repo, "split-panes")));
        var other = await on.Http.Ask(Get("/sindri/context/query?q=orientation", (SindriHeaders.Caller, "session"), (SindriHeaders.Repo, "other")));

        Assert.Equal((HttpStatusCode.Forbidden, "SND-CONTEXT-020"), (gated.Status, gated.Code));
        Assert.Equal(HttpStatusCode.Forbidden, origin.Status);
        Assert.Equal(HttpStatusCode.OK, own.Status);
        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-003"), (other.Status, other.Code));
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task An_anchor_or_an_empty_question_is_answered_with_its_code()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Approved(sessions: false, ct);

        var anchor = await host.Http.Ask(Get("/sindri/context/query?item=ENG-1", Human(host.Token.Value)));
        var node = await host.Http.Ask(Get("/sindri/context/query?node=feature:x", Human(host.Token.Value)));
        var empty = await host.Http.Ask(Get("/sindri/context/query?q=%20", Human(host.Token.Value)));

        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-003"), (anchor.Status, anchor.Code));   // item anchors arrive with S-5B
        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-030"), (node.Status, node.Code));   // a node anchor needs a map
        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-006"), (empty.Status, empty.Code));
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_window_reads_its_page_and_counts_every_match_and_a_bad_window_is_006()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Approved(sessions: false, ct);

        var page = await host.Http.Ask(Get("/sindri/context/query?q=orientation%20toggle&limit=1", Human(host.Token.Value)));
        var whole = await host.Http.Ask(Get("/sindri/context/query?q=orientation%20toggle", Human(host.Token.Value)));
        var zero = await host.Http.Ask(Get("/sindri/context/query?q=orientation&limit=0", Human(host.Token.Value)));
        var negative = await host.Http.Ask(Get("/sindri/context/query?q=orientation&offset=-1", Human(host.Token.Value)));
        var word = await host.Http.Ask(Get("/sindri/context/query?q=orientation&limit=ten", Human(host.Token.Value)));

        Assert.Equal(HttpStatusCode.OK, page.Status);
        using var paged = JsonDocument.Parse(page.Body);
        var matches = paged.RootElement.GetProperty("matches");
        Assert.Equal((0, 1), (matches.GetProperty("offset").GetInt32(), matches.GetProperty("read").GetInt32()));
        Assert.True(matches.GetProperty("matched").GetInt32() >= 1);
        using var all = JsonDocument.Parse(whole.Body);
        Assert.False(all.RootElement.TryGetProperty("matches", out _));   // no window: every match is read and nothing is counted apart
        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-006"), (zero.Status, zero.Code));
        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-006"), (negative.Status, negative.Code));
        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-006"), (word.Status, word.Code));
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Neither_the_question_nor_record_text_reaches_a_log_line()
    {
        var ct = TestContext.Current.CancellationToken;
        var sink = new InMemoryLogSink();
        var runtime = TelemetryRuntime.Start(new TelemetryOptions { ServiceName = "sindri", Sink = sink, Clock = TimeProvider.System, MinimumLevel = LogLevel.Debug });
        await using var host = await Approved(sessions: false, ct, services => services
            .Replace(ServiceDescriptor.Singleton(runtime))
            .Replace(ServiceDescriptor.Singleton(runtime.LoggerFactory)));

        var answer = await host.Http.Ask(Get("/sindri/context/query?q=zebra%20quokka%20orientation", Human(host.Token.Value)));
        runtime.Flush();

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Contains("focused pane", answer.Body, StringComparison.Ordinal);   // the decision doc's text came back …
        Assert.NotEmpty(sink.Lines);                                             // … and the host did log
        Assert.DoesNotContain("zebra", sink.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("focused pane", sink.Text, StringComparison.Ordinal);
    }

    private Task<GoldenHost> Approved(bool sessions, CancellationToken ct, Action<IServiceCollection>? extra = null) =>
        ContextHosts.Approved(_repo, sessions, ct, extra);
}
