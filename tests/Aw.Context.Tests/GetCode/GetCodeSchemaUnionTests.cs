using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Code;
using Aw.Context.Core.Lenses;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.GetCode;

/// <summary>The schema step asks git once per chunk of paths for all the data things together, and each step times out on its own (review run 2: I6 and the per-step time-out).</summary>
public sealed class GetCodeSchemaUnionTests
{
    private static bool IsSchemaQuery(CodeSearchQuery q) => q.Within is { } within && within.Any(p => p.StartsWith("migrations/", StringComparison.Ordinal));

    private static SearchedSource Row(CodeView view, string id) => Assert.Single(view.Searched.Sources, s => string.Equals(s.Id.Value, id, StringComparison.Ordinal));

    private static (string[] Nodes, string[] Edges) Gadgets(int count) =>
    (
        [.. Enumerable.Range(0, count).Select(i => $"  - {{key: \"thing:gadget-{i.ToString("D3", CultureInfo.InvariantCulture)}\", label: \"Gadget {i.ToString("D3", CultureInfo.InvariantCulture)}\"}}")],
        [.. Enumerable.Range(0, count).Select(i => $"  - {{from: \"action:sharing.create\", to: \"thing:gadget-{i.ToString("D3", CultureInfo.InvariantCulture)}\", type: writes}}")]
    );

    [Fact, Trait("Lane", "unit")]
    public async Task Several_data_things_are_searched_with_one_call_per_chunk_of_schema_paths()
    {
        var (nodes, edges) = Gadgets(5);
        using var world = new CodeWorld();
        _ = await world.ImportSharing(extraNodes: nodes, extraEdges: edges);

        var view = (await world.Code()).Value;

        Assert.Equal(1, world.Search.Queries.Count(IsSchemaQuery));
        Assert.Equal("migrations/002_sharing.sql", Assert.Single(view.Data.Files).Path);   // still tied to its own thing
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_schema_search_that_finishes_in_time_stays_ok_however_many_things_there_are()
    {
        var (nodes, edges) = Gadgets(5);
        var schemaCalls = 0;
        using var world = new CodeWorld(decorateTimed: (inner, expire) => new StallingSearch(inner, q => IsSchemaQuery(q) && ++schemaCalls > 1, expire));   // a second schema call would never answer
        _ = await world.ImportSharing(extraNodes: nodes, extraEdges: edges);

        var view = (await world.Code(MapWorld.HumanWith("{\"deadline\":\"100ms\",\"code\":{\"testGlobs\":" + CodeWorld.TestGlobs + "}}"))).Value;

        Assert.Equal(SearchedState.Ok, Row(view, "lexical-schema").State);
        _ = Assert.Single(view.Data.Files);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task When_only_the_schema_step_is_cut_only_the_schema_row_is_degraded()
    {
        using var world = new CodeWorld(decorateTimed: (inner, expire) => new StallingSearch(inner, IsSchemaQuery, expire));
        _ = await world.ImportSharing();

        var view = (await world.Code(MapWorld.HumanWith("{\"deadline\":\"100ms\",\"code\":{\"testGlobs\":" + CodeWorld.TestGlobs + "}}"))).Value;

        var tests = Row(view, "lexical-tests");
        var schema = Row(view, "lexical-schema");
        Assert.Equal((SearchedState.Ok, null), (tests.State, tests.Code));
        Assert.Equal((SearchedState.Degraded, ContextErrors.CodeSearchTimedOut.Code), (schema.State, schema.Code));
    }
}
