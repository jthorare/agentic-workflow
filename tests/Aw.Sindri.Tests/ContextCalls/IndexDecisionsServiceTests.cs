using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Tests.Workflow.Blocks.Input;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The 'index-decisions' service (#1597) over a fake <see cref="Context.Core.Sync.IIndexSync"/> behind the engine facade.</summary>
public sealed class IndexDecisionsServiceTests
{
    private const string OnyxSection = """{"sources":{"onyx":{"url":"http://127.0.0.1:3189","token":"on_live-key-0123","version":"v4.9.0"}}}""";
    private static readonly DateTimeOffset _now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    private readonly SliceHarness _h = new();
    private readonly FakeIndexSync _index = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Result<ScheduledResult>> Run(bool context)
    {
        _h.Clock.SetUtcNow(_now);
        var loaded = SliceHarness.Profile("5m");
        loaded = loaded with { Profile = loaded.Profile with { Context = context ? JsonDocument.Parse(OnyxSection).RootElement.Clone() : null } };
        _ = await _h.Profiles.Save(loaded, Ct);
        var grant = await _h.HoldLease();
        using var caller = _h.Callers.EnterService("index-decisions");
        return await new IndexDecisionsService(new ContextScopes(_h.Profiles, _h.Callers), EngineProxy.Of(_index)).Run(new ScheduledRun(grant, _now), Ct);
    }

    private static SourceDecisions Row(string source, int looked, int decisions, string? code = null) => new(SourceId.From(source), looked, decisions, code);

    [Fact, Trait("Lane", "unit")]
    public void IndexDecisions_is_a_fifteen_minute_built_in()
    {
        Assert.Equal("index-decisions", IndexDecisionsService.BuiltIn.Name.Value);
        Assert.Equal(TimeSpan.FromMinutes(15), IndexDecisionsService.BuiltIn.Cadence.Interval);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Run_reports_each_source_that_was_read()
    {
        _index.Decided = [Row("github", 4, 2), Row("linear", 0, 0, ContextErrors.LabelingUnavailable.Code)];

        var result = await Run(context: true);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _index.Decides);
        Assert.False(result.Value.Idle);
        Assert.Equal($"ok: github 4 read, 2 decisions, linear {ContextErrors.LabelingUnavailable.Code}", result.Value.Summary);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Run_with_nothing_new_to_read_is_quiet()
    {
        _index.Decided = [Row("github", 0, 5)];

        var result = await Run(context: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Idle);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Run_without_a_context_section_does_not_call_the_pass()
    {
        var result = await Run(context: false);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Idle);
        Assert.Equal(0, _index.Decides);
    }
}
