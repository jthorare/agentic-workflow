using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;

namespace Aw.Context.Tests.Extract;

public sealed class SharedFileClusterTests
{
    private static readonly ContextRepoName _repo = ContextRepoName.From("web-app");

    private static ClusterSet Set(params FileCluster[] clusters) => new(clusters, Available: true);

    private static FileCluster Cluster(string id, double confidence, params string[] files) => new(ClusterId.From(id), files, confidence);

    [Fact, Trait("Lane", "unit")]
    public void Shared_files_icons_routers_crons_and_tests_are_not_feature_implementers()
    {
        var map = ClusterFeatures.Propose(_repo, Set(Cluster("c1", 0.55,
            "apps/web/src/Staff/SelfScheduling/schedule.tsx",
            "packages/icons/src/ArrowsDownUp/Fill.tsx",
            "packages/ui-deprecated/src/components/ui/combobox.tsx",
            "apps/web/src/AppShell/components/ChatWidget.tsx",
            "apps/server/src/api/api-router.ts",
            "apps/server/src/crons/deactivate-profile-cron.ts",
            "apps/web/src/CentralStaffing/Demo/ActivitySidebar/__tests__/demo-open-shift-post.unit.test.ts")), [], ExtractedMap.Empty);

        var edges = map.Edges.Where(e => e.Type == EdgeType.Implements).ToList();
        Assert.Equal(["code:apps/web/src/Staff/SelfScheduling/schedule.tsx"], edges.Select(e => e.From.Value), StringComparer.Ordinal);
    }
}
