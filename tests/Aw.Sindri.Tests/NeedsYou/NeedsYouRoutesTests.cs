using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.NeedsYou;

/// <summary>The deep links a needs-you 'open' names (q65), each in the app's DeepLink form.</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouRoutesTests
{
    private static readonly InstanceAddress _root = InstanceAddress.From("ITM-" + new string('1', 32) + "/line/1");
    private static readonly InstanceAddress _deepest = InstanceAddress.From(_root.Value + "/scoping.1/step.2");

    [Fact]
    public void A_workflow_route_is_workflow_and_the_address() => Assert.Equal("workflow/" + _root.Value, NeedsYouRoutes.Workflow(_root));

    [Fact]
    public void A_desk_opens_its_instance() => Assert.Equal("workflow/" + _root.Value, NeedsYouRoutes.Desk(_root));

    [Fact]
    public void A_park_opens_its_deepest_parked_descendant() => Assert.Equal("workflow/" + _deepest.Value, NeedsYouRoutes.Park(_root, _deepest));

    [Fact]
    public void A_park_the_root_decides_opens_the_root() => Assert.Equal("workflow/" + _root.Value, NeedsYouRoutes.Park(_root, deepest: null));

    [Fact]
    public void A_decision_is_decision_and_its_id() => Assert.Equal("decision/" + A.FirstDecisionId.Value, NeedsYouRoutes.Decision(A.FirstDecisionId));

    [Fact]
    public void A_service_is_service_and_its_name() => Assert.Equal("service/onyx", NeedsYouRoutes.Service("onyx"));

    [Fact]
    public void A_recipe_is_recipe_and_its_id_with_no_hash() => Assert.Equal("recipe/workflow.pr-babysitter", NeedsYouRoutes.Recipe(RecipeId.From("workflow.pr-babysitter")));

    [Fact]
    public void A_row_with_no_other_place_opens_its_own_sheet() => Assert.Equal("needs-you/profile:drift", NeedsYouRoutes.Row(NeedsYouRowId.From("profile:drift")));

    [Fact]
    public void Setup_is_the_setup_screen() => Assert.Equal("setup", NeedsYouRoutes.Setup);
}
