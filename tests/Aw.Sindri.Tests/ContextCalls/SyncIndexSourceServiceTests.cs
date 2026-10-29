using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Workflow.Blocks.Input;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The named index sync job (#1597): it asks the engine for that one source's tick and nothing else, and a source's code is its result, not a failed job.</summary>
public sealed class SyncIndexSourceServiceTests
{
    private const string OnyxSection = """{"sources":{"onyx":{"url":"http://127.0.0.1:3189","token":"on_live-key-0123","version":"v4.9.0"}}}""";
    private readonly SliceHarness _h = new();
    private readonly FakeIndexSync _index = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SourceSync Row(string source, int fetched, string? code = null) =>
        new(SourceId.From(source), Records: fetched, fetched, Unchanged: 0, Linked: 0, Unlinked: 0, Orphaned: 0, Decisions: 0, Queued: 0, Refused: 0, Tokens: 0, code);

    private async Task<Result<SyncIndexSourceResult>> Run(string source, bool context = true)
    {
        var loaded = SliceHarness.Profile("5m");
        loaded = loaded with { Profile = loaded.Profile with { Context = context ? JsonDocument.Parse(OnyxSection).RootElement.Clone() : null } };
        _ = await _h.Profiles.Save(loaded, Ct);
        using var caller = _h.Callers.EnterService("index-sync");
        return await new SyncIndexSourceService(new ContextScopes(_h.Profiles, _h.Callers), EngineProxy.Of(_index), new FakeJobContextAccessor()).Handle(new SyncIndexSourceRequest(source), Ct);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_named_source_is_one_named_tick_and_only_its_row_is_returned()
    {
        _index.Rows = [Row("linear", 12), Row("github", 3)];

        var result = await Run("github");

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        Assert.Equal(new SyncIndexSourceResult("github", 3, 3, Code: null), result.Value);
        Assert.Equal(1, _index.Ticks);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_source_that_ended_with_a_code_is_a_result_with_the_code_not_a_failed_job()
    {
        _index.Rows = [Row("github", 0, ContextErrors.SyncRunning.Code)];

        var result = await Run("github");

        Assert.True(result.IsSuccess);
        Assert.Equal(ContextErrors.SyncRunning.Code, result.Value.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_name_that_is_not_a_source_id_ticks_nothing()
    {
        var result = await Run("Not A Source");

        Assert.True(result.IsError);
        Assert.Equal(0, _index.Ticks);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_profile_without_a_context_section_ticks_nothing()
    {
        var result = await Run("github", context: false);

        Assert.True(result.IsError);
        Assert.Equal(0, _index.Ticks);
    }
}
