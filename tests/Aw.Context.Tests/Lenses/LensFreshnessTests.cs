using System.Globalization;
using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Lenses;

/// <summary>#1373: a lens answer says how old each record is and how fresh the owned index's sources are.</summary>
public sealed class LensFreshnessTests : IDisposable
{
    private const string Anchor = "feature:in";
    private const string Ticket = "record:linear:issue/0e5a1b00-0000-4000-8000-000000000a02";
    private const string Doc = "record:repo-docs:docs/panes.md";
    private static readonly DateTimeOffset _ticketAt = DateTimeOffset.Parse("2026-10-10T08:15:00Z", CultureInfo.InvariantCulture);
    private static readonly DateTimeOffset _docAt = DateTimeOffset.Parse("2026-09-30T17:40:00Z", CultureInfo.InvariantCulture);
    private static readonly DateTimeOffset _githubSync = DateTimeOffset.Parse("2026-10-28T10:00:00Z", CultureInfo.InvariantCulture);

    private readonly FakeIndexSync _index = new();
    private readonly MapWorld _world;

    public LensFreshnessTests()
    {
        _world = new MapWorld(indexSync: _index);
    }

    public void Dispose() => _world.Dispose();

    private async Task<LensesView> Lens()
    {
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"feature:in\", label: \"In\"}",
            $"  - {{key: \"{Ticket}\", label: \"Tab spec\", at: \"{Iso(_ticketAt)}\"}}",
            $"  - {{key: \"{Doc}\", label: \"Panes doc\", at: \"{Iso(_docAt)}\"}}",
            "edges:",
            $"  - {{from: \"{Ticket}\", to: \"feature:in\", type: mentions}}",
            $"  - {{from: \"{Doc}\", to: \"feature:in\", type: mentions}}"));
        var view = await _world.Engine.Lenses(Anchor, MapRead.Current, depth: null, MapWorld.Human, MapWorld.None);
        Assert.True(view.IsSuccess, view.IsError ? view.Error.Message : null);
        return view.Value;
    }

    private static string Iso(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    /// <summary>The "at" of a record, read from the JSON the lens answer serializes to (LensRecord has no At property yet), or null when it has none.</summary>
    private static string? AtOf(LensesView view, string key)
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(view));
        var records = json.RootElement.EnumerateObject().First(p => p.NameEquals("bands") || p.Name.Equals("bands", StringComparison.OrdinalIgnoreCase)).Value
            .EnumerateObject().First(p => p.Name.Equals("intent", StringComparison.OrdinalIgnoreCase)).Value
            .EnumerateObject().First(p => p.Name.Equals("records", StringComparison.OrdinalIgnoreCase)).Value;
        var record = records.EnumerateArray().Single(r => r.EnumerateObject().Any(p => p.Name.Equals("key", StringComparison.OrdinalIgnoreCase) && p.Value.GetString() == key));
        foreach (var property in record.EnumerateObject().Where(p => p.Name.Equals("at", StringComparison.OrdinalIgnoreCase)))
        {
            return property.Value.GetString();
        }

        return null;
    }

    private static string Serialized(DateTimeOffset at) => JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(at))!;

    private static SourceProgress Row(string source, SyncPhase? phase, DateTimeOffset? lastSync, string? lastCode) =>
        SourceProgress.From(SourceId.From(source), phase, lastSync, lastCode);

    private static SearchedSource Searched(LensesView view, string id) =>
        Assert.Single(view.Searched.Sources, s => s.Id.Value == id);

    [Fact, Trait("Lane", "unit")]
    public async Task A_tracker_record_carries_its_stored_update_time_and_a_doc_record_its_head_commit_time()
    {
        var view = await Lens();

        Assert.Equal(Serialized(_ticketAt), AtOf(view, Ticket));
        Assert.Equal(Serialized(_docAt), AtOf(view, Doc));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_lens_lists_each_owned_index_source_with_its_sync_state()
    {
        _index.Standing =
        [
            Row("linear", SyncPhase.Incremental, _githubSync, ContextErrors.SourceUnavailable.Code),
            Row("github", SyncPhase.Incremental, _githubSync, lastCode: null),
            Row("repo-docs", phase: null, lastSync: null, lastCode: null),
        ];

        var view = await Lens();

        var linear = Searched(view, "linear");
        Assert.Equal((SearchedState.Degraded, ContextErrors.SourceUnavailable.Code), (linear.State, linear.Code));
        var github = Searched(view, "github");
        Assert.Equal((SearchedState.Ok, (DateTimeOffset?)_githubSync), (github.State, github.AsOf));
        Assert.Equal(SearchedState.Unavailable, Searched(view, "repo-docs").State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_source_whose_last_tick_hit_the_time_cap_is_ok_not_degraded()
    {
        _index.Standing = [Row("linear", SyncPhase.Incremental, _githubSync, ContextErrors.SyncCapped.Code)];

        var view = await Lens();

        Assert.Equal(SearchedState.Ok, Searched(view, "linear").State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_onyx_in_the_profile_the_lens_has_no_onyx_row()
    {
        var view = await Lens();

        Assert.DoesNotContain(view.Searched.Sources, s => s.Id.Value == "onyx");
    }
}
