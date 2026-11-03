using Aw.Context.Core;
using Aw.Context.Core.Map;
using Aw.Context.Testing.Engine;
using Aw.Kernel;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Tests.Workflow.Blocks.Input;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The 'context-upkeep' service (#1953) over a fake engine answering each shape of <see cref="StoreUpkeep"/>.</summary>
public sealed class ContextUpkeepServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private readonly SliceHarness _h = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Result<ScheduledResult>> Run(Func<Result<StoreUpkeep?>> upkeep)
    {
        _h.Clock.SetUtcNow(_now);
        var grant = await _h.HoldLease();
        return await new ContextUpkeepService(EngineProxy.Of(FakeContextEngine.WithUpkeep(upkeep))).Run(new ScheduledRun(grant, _now), Ct);
    }

    [Fact, Trait("Lane", "unit")]
    public void ContextUpkeep_is_a_fifteen_minute_built_in()
    {
        Assert.Equal("context-upkeep", ContextUpkeepService.BuiltIn.Name.Value);
        Assert.Equal(TimeSpan.FromMinutes(15), ContextUpkeepService.BuiltIn.Cadence.Interval);
        Assert.Equal("Checkpoints context.db's log and refreshes its query stats.", ContextUpkeepService.BuiltIn.Description);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_store_is_a_quiet_run()
    {
        var result = await Run(() => Results.Success<StoreUpkeep?>(value: null));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Idle);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_empty_log_is_a_quiet_run()
    {
        var result = await Run(() => Results.Success<StoreUpkeep?>(new StoreUpkeep(Optimized: true, Truncated: true, LogPages: 0, CheckpointedPages: 0)));

        Assert.True(result.Value.Idle);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_truncated_log_says_how_many_pages_its_file_held()
    {
        var result = await Run(() => Results.Success<StoreUpkeep?>(new StoreUpkeep(Optimized: true, Truncated: true, LogPages: 3200, CheckpointedPages: 3200)));

        Assert.False(result.Value.Idle);
        Assert.Equal("ok: log file truncated (it held 3200 pages)", result.Value.Summary);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_log_held_by_a_reader_is_partial_and_not_an_error()
    {
        var result = await Run(() => Results.Success<StoreUpkeep?>(new StoreUpkeep(Optimized: true, Truncated: false, LogPages: 3200, CheckpointedPages: 1200)));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Idle);
        Assert.Equal("partial: a reader or writer held the log (1200 of the log file's 3200 pages checkpointed)", result.Value.Summary);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_store_that_cant_be_maintained_is_the_runs_error()
    {
        var result = await Run(() => ContextErrors.StoreUnavailable.With("AW-DATA-001: locked"));

        Assert.True(result.IsError);
        Assert.Equal("SND-CONTEXT-004", result.Error.Code);
    }
}
