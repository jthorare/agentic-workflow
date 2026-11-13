using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Map;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Host;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The index status carries doctor's context:store row as its 'store' field (#1963), and a failed store check still lets the status answer.</summary>
public sealed class IndexStatusStoreTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    private readonly SliceHarness _h = new();
    private readonly FakeIndexSync _index = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Result<IndexStatusView>> Status(Result<MapHealth> store)
    {
        _ = await _h.Profiles.Save(SliceHarness.Profile("5m"), Ct);
        _index.Standing = [SourceProgress.From(SourceId.From("linear"), SyncPhase.Incremental, _now, lastCode: null)];
        using var caller = _h.Callers.EnterService("test");
        return await new GetIndexStatusService(new ContextScopes(_h.Profiles, _h.Callers, new FakeRepoCheckouts()), IndexStoreEngineProxy.Create(_index, store)).Handle(new GetIndexStatusRequest(), Ct);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_carries_an_ok_store_row_with_no_fix()
    {
        var view = await Status(new MapHealth("context:store", MapHealthState.Ok, "context.db 2.4 GB, its log 12.0 MB", Fix: null));

        Assert.True(view.IsSuccess);
        Assert.Equal(new IndexStoreView("ok", "context.db 2.4 GB, its log 12.0 MB", Fix: null), view.Value.Store);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_carries_a_warn_store_row_with_its_detail_and_fix()
    {
        var view = await Status(new MapHealth("context:store", MapHealthState.Warn, "context.db 41 GB, its log 3.0 GB", "sindri context checkpoint"));

        Assert.Equal(new IndexStoreView("warn", "context.db 41 GB, its log 3.0 GB", "sindri context checkpoint"), view.Value.Store);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_whose_store_check_failed_still_answers_with_a_fail_row_led_by_the_code()
    {
        var view = await Status(ContextErrors.StoreUnavailable.With("the store can't be opened"));

        Assert.True(view.IsSuccess);
        Assert.Equal("linear", Assert.Single(view.Value.Sources).Source);
        Assert.NotNull(view.Value.Concepts);
        var store = view.Value.Store!;
        Assert.Equal("fail", store.State);
        Assert.StartsWith(ContextErrors.StoreUnavailable.Code, store.Detail, StringComparison.Ordinal);
        Assert.Equal("check $AW_SINDRI_HOME/context", store.Fix);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_json_has_store_after_concepts_with_state_detail_and_fix()
    {
        var view = await Status(new MapHealth("context:store", MapHealthState.Ok, "context.db 2.4 GB, its log 12.0 MB", Fix: null));

        using var json = JsonDocument.Parse(AwJson.Serialize(view.Value, AwJson.CreateOptions(SindriJsonContext.Default)));
        var names = json.RootElement.EnumerateObject().Select(static p => p.Name).ToList();
        Assert.Equal(names.IndexOf("concepts") + 1, names.IndexOf("store"));
        var store = json.RootElement.GetProperty("store");
        Assert.Equal("state,detail,fix", string.Join(',', store.EnumerateObject().Select(static p => p.Name)));
        Assert.Equal("ok", store.GetProperty("state").GetString());
        Assert.Equal("context.db 2.4 GB, its log 12.0 MB", store.GetProperty("detail").GetString());
        Assert.Equal(JsonValueKind.Null, store.GetProperty("fix").ValueKind);
    }
}
