using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Vectors;
using Aw.Context.Testing.Engine;
using Aw.Kernel;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The 'index-embed' service (owned-index spec §5.6) over a fake context engine whose embed runs answer from the test.</summary>
public sealed class IndexEmbedServiceTests
{
    private const string Section = """{"sources":{"linear":{"teams":["SPL"]}}}""";
    private static readonly DateTimeOffset _now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    private readonly SliceHarness _h = new();
    private readonly List<EmbedRecordsRequest> _asked = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Result<ScheduledResult>> Run(bool context, Func<EmbedRecordsRequest, Result<EmbedRecordsReport>> answer, int? records = null)
    {
        _h.Clock.SetUtcNow(_now);
        var loaded = SliceHarness.Profile("1m");
        loaded = loaded with { Profile = loaded.Profile with { Context = context ? JsonDocument.Parse(Section).RootElement.Clone() : null } };
        _ = await _h.Profiles.Save(loaded, Ct);
        var grant = await _h.HoldLease();
        using var caller = _h.Callers.EnterService("index-embed");
        Result<EmbedRecordsReport> Ask(EmbedRecordsRequest request)
        {
            _asked.Add(request);
            return answer(request);
        }

        var stub = EmbedEngineProxy.Create();
        stub.Records = records;
        var engine = records is null ? FakeContextEngine.WithEmbed(Ask) : stub.Proxy;
        if (records is not null)
        {
            stub.Report = Ask(new EmbedRecordsRequest());
        }

        return await new IndexEmbedService(new ContextScopes(_h.Profiles, _h.Callers, new FakeRepoCheckouts()), engine).Run(new ScheduledRun(grant, _now), Ct);
    }

    [Fact, Trait("Lane", "unit")]
    public void IndexEmbed_is_a_one_minute_built_in()
    {
        Assert.Equal("index-embed", IndexEmbedService.BuiltIn.Name.Value);
        Assert.Equal(TimeSpan.FromMinutes(1), IndexEmbedService.BuiltIn.Cadence.Interval);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_run_embeds_one_chunk_of_work_and_says_how_many()
    {
        var result = await Run(context: true, _ => new EmbedRecordsReport(4096, 4090, Done: false));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Idle);
        Assert.Equal("ok: embedded 4096 chunks (4090 stored), more to embed", result.Value.Summary);
        Assert.Equal([new EmbedRecordsRequest()], _asked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_run_that_finishes_the_backlog_says_none_left()
    {
        var result = await Run(context: true, _ => new EmbedRecordsReport(12, 12, Done: true));

        Assert.Equal("ok: embedded 12 chunks (12 stored), none left", result.Value.Summary);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Nothing_to_embed_is_quiet()
    {
        var result = await Run(context: true, _ => new EmbedRecordsReport(0, 0, Done: true));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Idle);
        Assert.Null(result.Value.Summary);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_model_that_is_down_is_a_state_not_a_failed_run()
    {
        var result = await Run(context: true, _ => ContextErrors.EmbedderUnavailable.With("AW-EMBEDDINGS-004: ollama is not running"));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Idle);
        Assert.StartsWith("meaning search is off", result.Value.Summary, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Any_other_error_fails_the_run()
    {
        var result = await Run(context: true, _ => ContextErrors.StoreUnavailable.With("the record store is locked"));

        Assert.True(result.IsError);
        Assert.Equal(ContextErrors.StoreUnavailable.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_context_section_the_engine_is_asked_and_an_empty_index_says_nothing_indexed_yet()
    {
        var result = await Run(context: false, _ => new EmbedRecordsReport(0, 0, Done: true), records: 0);

        Assert.True(result.IsSuccess);
        Assert.Equal([new EmbedRecordsRequest()], _asked);
        Assert.True(result.Value.Idle);
        Assert.Equal("nothing indexed yet", result.Value.Summary);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_caught_up_index_with_nothing_newly_embedded_stays_quiet()
    {
        var result = await Run(context: true, _ => new EmbedRecordsReport(0, 0, Done: true), records: 120);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Idle);
        Assert.Null(result.Value.Summary);
    }
}
