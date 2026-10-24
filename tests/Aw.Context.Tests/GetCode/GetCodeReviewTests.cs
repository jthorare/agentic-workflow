using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Code;
using Aw.Context.Core.Lenses;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.GetCode;

/// <summary>The code lens's caps and failure rows at the service level (review run 1: I1, I2, I6, I8, I9).</summary>
public sealed class GetCodeReviewTests
{
    private static SearchedSource Row(CodeView view, string id) => Assert.Single(view.Searched.Sources, s => string.Equals(s.Id.Value, id, StringComparison.Ordinal));

    private static string N(int i) => i.ToString("D3", CultureInfo.InvariantCulture);

    [Fact, Trait("Lane", "unit")]
    public async Task More_than_100_candidate_tests_truncate_the_tests_list_and_the_row_says_capped()
    {
        // No edge test and no other test file, so the list holds exactly the 100 candidates read: only the scan's own cap says the answer is cut.
        var files = CodeWorld.Files.Where(f => !f.Key.StartsWith("tests/", StringComparison.Ordinal)).ToDictionary(f => f.Key, f => f.Value, StringComparer.Ordinal);
        for (var i = 0; i < CodeRules.MaxCandidates + 50; i++)
        {
            files[$"tests/gen/g{N(i)}.test.ts"] = "import { x } from '../../src/sharing/links';";
        }

        using var world = new CodeWorld(files);
        _ = await world.ImportSharing(edgeTest: false);

        var view = (await world.Code()).Value;

        Assert.True(view.Truncated.Tests);
        Assert.Equal((SearchedState.Ok, "capped"), (Row(view, "lexical-tests").State, Row(view, "lexical-tests").Reason));
        Assert.Equal(CodeRules.MaxCandidates, view.Tests.Count);   // the candidate scan is capped; the list itself is not cut
    }

    [Fact, Trait("Lane", "unit")]
    public async Task More_than_100_schema_files_for_a_thing_truncate_the_schema_list_and_the_row_says_capped()
    {
        var files = new Dictionary<string, string>(CodeWorld.Files, StringComparer.Ordinal);
        for (var i = 0; i < CodeRules.MaxFilesPerThing + 10; i++)
        {
            files[$"migrations/m{N(i)}.sql"] = "create table shared_link (id int);";
        }

        using var world = new CodeWorld(files);
        _ = await world.ImportSharing();

        var view = (await world.Code()).Value;

        Assert.True(view.Truncated.SchemaFiles);
        Assert.Equal((SearchedState.Ok, "capped"), (Row(view, "lexical-schema").State, Row(view, "lexical-schema").Reason));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task More_than_100_searchable_things_are_reported_as_capped_not_dropped_silently()
    {
        var nodes = Enumerable.Range(0, CodeRules.MaxThings + 1).Select(i => $"  - {{key: \"thing:gadget-{N(i)}\", label: \"Gadget {N(i)}\"}}").ToArray();
        var edges = Enumerable.Range(0, CodeRules.MaxThings + 1).Select(i => $"  - {{from: \"action:sharing.create\", to: \"thing:gadget-{N(i)}\", type: writes}}").ToArray();
        using var world = new CodeWorld();
        _ = await world.ImportSharing(extraNodes: nodes, extraEdges: edges);

        var view = (await world.Code()).Value;

        Assert.True(view.Truncated.SchemaFiles);
        Assert.Equal((SearchedState.Ok, "capped"), (Row(view, "lexical-schema").State, Row(view, "lexical-schema").Reason));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task When_the_tests_step_uses_the_deadline_the_schema_row_claims_no_read()
    {
        using var slow = new CodeWorld(decorateTimed: (inner, expire) => new StallingSearch(inner, _ => true, expire));
        _ = await slow.ImportSharing();

        var view = (await slow.Code(MapWorld.HumanWith("""{"deadline":"100ms"}"""))).Value;

        var tests = Row(view, "lexical-tests");
        var schema = Row(view, "lexical-schema");
        Assert.Equal((SearchedState.Degraded, ContextErrors.CodeSearchTimedOut.Code), (tests.State, tests.Code));
        _ = Assert.NotNull(tests.AsOf);
        Assert.Equal((SearchedState.Degraded, ContextErrors.CodeSearchTimedOut.Code), (schema.State, schema.Code));
        Assert.Null(schema.AsOf);   // nothing was read for it
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_search_failure_degrades_only_its_own_source_with_its_own_code()
    {
        using var world = new CodeWorld(decorate: inner => new FailingSearch(inner, q => q.Terms.Any(t => t.Contains("sharing/", StringComparison.Ordinal))));
        _ = await world.ImportSharing();

        var view = (await world.Code()).Value;

        var tests = Row(view, "lexical-tests");
        Assert.Equal((SearchedState.Degraded, ContextErrors.ExtractionUnreadable.Code), (tests.State, tests.Code));
        Assert.Equal(SearchedState.Ok, Row(view, "lexical-schema").State);
        _ = Assert.Single(view.Data.Files);   // the schema half still answered
        Assert.Equal(2, view.Files.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_commit_that_cannot_be_resolved_for_a_reason_other_than_missing_is_degraded_not_commit_not_held()
    {
        using var world = new CodeWorld(decorate: inner => new FailingSearch(inner, _ => false, resolveFails: true));
        _ = await world.ImportSharing(commit: MapWorld.Head);

        var view = (await world.Code()).Value;

        foreach (var id in new[] { "lexical-tests", "lexical-schema" })
        {
            var row = Row(view, id);
            Assert.Equal((SearchedState.Degraded, ContextErrors.ExtractionUnreadable.Code, null), (row.State, row.Code, row.Reason));
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_that_cannot_give_its_head_degrades_both_rows_with_048()
    {
        using var world = new CodeWorld(decorateRepo: inner => new FailingRepo(inner, head: true, list: false));
        _ = await world.ImportSharing();

        var view = (await world.Code()).Value;

        foreach (var id in new[] { "lexical-tests", "lexical-schema" })
        {
            Assert.Equal((SearchedState.Degraded, ContextErrors.ExtractionUnreadable.Code), (Row(view, id).State, Row(view, id).Code));
        }

        Assert.Equal(2, view.Files.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_that_cannot_list_its_files_is_degraded_not_commit_not_held()
    {
        using var world = new CodeWorld(decorateRepo: inner => new FailingRepo(inner, head: false, list: true));
        _ = await world.ImportSharing();

        var view = (await world.Code()).Value;

        var row = Row(view, "lexical-tests");
        Assert.Equal((SearchedState.Degraded, ContextErrors.ExtractionUnreadable.Code, null), (row.State, row.Code, row.Reason));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_no_evidence_for_any_implementing_file_the_map_repo_is_still_searched_and_files_are_unlocated()
    {
        using var world = new CodeWorld();
        _ = await world.World.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"feature:sharing\", label: \"Sharing\"}",
            "  - {key: \"surface:screen/share\", label: \"Share\"}",
            "  - {key: \"action:sharing.create\", label: \"Create a link\"}",
            "  - {key: \"thing:shared-link\", label: \"Shared link\"}",
            "  - {key: \"code:src/sharing/links.ts\", label: \"links.ts\", provenance: deterministic}",
            "edges:",
            "  - {from: \"surface:screen/share\", to: \"feature:sharing\", type: part-of}",
            "  - {from: \"action:sharing.create\", to: \"surface:screen/share\", type: on}",
            "  - {from: \"action:sharing.create\", to: \"thing:shared-link\", type: writes}",
            "  - {from: \"code:src/sharing/links.ts\", to: \"feature:sharing\", type: implements, provenance: deterministic}"));

        var view = (await world.Code()).Value;

        Assert.Equal(1, view.Counts.Unlocated);
        Assert.Equal(SearchedState.Ok, Row(view, "lexical-schema").State);
        _ = Assert.Single(view.Data.Files);   // the schema scan ran on the map's repo
        Assert.Empty(view.Tests);
    }
}
