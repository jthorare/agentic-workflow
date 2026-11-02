using Aw.Context.Testing.Fixtures;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.GetCode;

// The map build's data-store edges reach the code lens: the code files that implement a feature show the things they read or write, each with its exact access.
public sealed class GetCodeDataStoresTests
{
    private static readonly string _map = string.Join('\n',
    [
        "schemaVersion: 1",
        "kind: authored",
        "repo: aw-split-panes",
        "nodes:",
        "  - {key: \"feature:sharing\", label: \"Sharing\"}",
        "  - {key: \"code:src/links/read.ts\", label: \"read.ts\", provenance: deterministic}",
        "  - {key: \"code:src/links/write.ts\", label: \"write.ts\", provenance: deterministic}",
        "  - {key: \"code:src/billing/lines.ts\", label: \"lines.ts\", provenance: deterministic}",
        "edges:",
        "  - {from: \"code:src/links/read.ts\", to: \"feature:sharing\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/links/read.ts\"}]}",
        "  - {from: \"code:src/links/write.ts\", to: \"feature:sharing\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/links/write.ts\"}]}",
        "  - {from: \"code:src/billing/lines.ts\", to: \"feature:sharing\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/billing/lines.ts\"}]}",
    ]) + "\n";

    [Fact, Trait("Lane", "unit")]
    public async Task A_feature_s_code_shows_the_data_stores_its_files_read_and_write_with_exact_access()
    {
        using var code = new CodeWorld(DataStoreRepo.Files);
        _ = await code.World.ImportYaml(_map);
        var built = await code.World.Map.BuildMap(progress: null, MapWorld.HumanWith(MapWorld.OnlyFixtureRepo), MapWorld.None);
        Assert.True(built.IsSuccess, built.IsError ? built.Error.Message : null);

        var view = (await code.Code(draft: true)).Value;

        Assert.Equal(
            [
                "thing:audit-event|Writes|code:src/links/write.ts",
                "thing:invoice-line|Both|code:src/billing/lines.ts",
                "thing:shared-link|Reads|code:src/links/read.ts",
            ],
            view.Data.Things.Select(t => $"{t.Key.Value}|{t.Access}|{string.Join(',', t.Via.Select(v => v.Value))}"),
            StringComparer.Ordinal);
    }
}
