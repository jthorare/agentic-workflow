using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Tests.Extract.Imports;

public sealed class ImportEdgesInMapTests
{
    private static readonly ContextRepoName _repo = ContextRepoName.From("aw-split-panes");

    private static readonly RepoFile[] _files =
    [
        new("src/commands/ids.ts", "export type CommandId = \"split-vertical\" | \"close-pane\";"),
        new("src/keys.ts", "export const keys: Record<string, CommandId> = { \"ctrl+shift+d\": \"split-vertical\" };"),
        new("src/ui/panel.ts", "import { keys } from '../keys';\nexport const panel = keys;"),
    ];

    [Fact, Trait("Lane", "unit")]
    public void The_map_carries_depends_on_edges_between_code_files_beside_the_implements_edges()
    {
        var map = MapExtractor.Extract(_repo, _files);

        var edge = Assert.Single(map.Edges, e => e.Type == EdgeType.DependsOn);
        Assert.Equal(("code:src/ui/panel.ts", "code:src/keys.ts", Provenance.Deterministic, 1.0), (edge.From.Value, edge.To.Value, edge.Provenance, edge.Confidence));
        Assert.Equal(["repo:aw-split-panes/src/ui/panel.ts"], edge.EvidenceOrEmpty.Select(e => e.Key.Value), StringComparer.Ordinal);
        Assert.Contains(map.Nodes, n => n.Key.Value == "code:src/ui/panel.ts");
        Assert.Contains(map.Edges, e => e.Type == EdgeType.Implements && e.From.Value == "code:src/keys.ts");
    }

    [Fact, Trait("Lane", "unit")]
    public void Code_may_depend_on_code_but_not_on_a_feature()
    {
        Assert.True(EdgeRules.Allows(EdgeType.DependsOn, NodeKind.Code, NodeKind.Code));
        Assert.True(EdgeRules.Allows(EdgeType.DependsOn, NodeKind.Feature, NodeKind.Feature));
        Assert.False(EdgeRules.Allows(EdgeType.DependsOn, NodeKind.Code, NodeKind.Feature));
        Assert.False(EdgeRules.Allows(EdgeType.DependsOn, NodeKind.Feature, NodeKind.Code));
    }
}
