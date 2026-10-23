using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.NeedsYou;

/// <summary>The deep links a needs-you 'open' names (q65 A2), each in the app's DeepLink form.</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouRoutesTests
{
    private static readonly InstanceAddress _root = InstanceAddress.From("ITM-" + new string('1', 32) + "/line/1");
    private static readonly InstanceAddress _deepest = InstanceAddress.From(_root.Value + "/scoping.1/step.2");

    [Fact]
    public void A_desk_opens_its_instance()
    {
        Assert.Equal("workflow/" + _root.Value, NeedsYouRoutes.Desk(_root));
    }

    [Fact]
    public void A_park_opens_its_deepest_parked_descendant()
    {
        Assert.Equal("workflow/" + _deepest.Value, NeedsYouRoutes.Park(_root, _deepest));
    }

    [Fact]
    public void A_park_the_root_decides_opens_the_root()
    {
        Assert.Equal("workflow/" + _root.Value, NeedsYouRoutes.Park(_root, deepest: null));
    }

    [Fact]
    public void The_other_routes_take_the_apps_deep_link_forms()
    {
        Assert.Equal(
            ("decision/" + A.FirstDecisionId.Value, "service/onyx", "recipe/workflow.pr-babysitter", "needs-you/profile:drift", "setup"),
            (NeedsYouRoutes.Decision(A.FirstDecisionId), NeedsYouRoutes.Service("onyx"), NeedsYouRoutes.Recipe(RecipeId.From("workflow.pr-babysitter")),
                NeedsYouRoutes.Row(NeedsYouRowId.From("profile:drift")), NeedsYouRoutes.Setup));
    }
}
