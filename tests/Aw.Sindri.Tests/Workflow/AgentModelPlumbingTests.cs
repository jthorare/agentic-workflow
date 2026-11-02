using System.Text.Json;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Infrastructure.Workflows;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>Issue 966 spec 6: a state's 'model' parameter reaches the launcher as the request's model. A model id is a literal; a '{ profile: … }' value is refused at launch with a clear reason.</summary>
public sealed class AgentModelPlumbingTests
{
    private static readonly AgentStepTarget _target = new(ItemId.From("ITM-" + new string('a', 32)), AgentId.From("claude-code"), "/repo");

    private static AgentStepRun Run()
    {
        var address = InstanceAddress.From("T-1/step/1");
        return new AgentStepRun(WorkflowInstanceId.Of(address), address, RunToken.From("1.1"), new StepRef("gap-search", "step.scope.gap-search"), WriteScope: false);
    }

    private static StepUse Step(params (string Name, WithValue Value)[] with) =>
        new(BlockId.From("step.scope.gap-search"), with.ToDictionary(static w => w.Name, static w => w.Value, StringComparer.Ordinal), []);

    [Fact, Trait("Lane", "unit")]
    public void A_model_on_the_extras_is_the_requests_model()
    {
        var request = AgentLaunches.RequestFor(Run(), _target, descriptor: null, new AgentLaunchExtras(Model: "claude-opus-5-5"));

        Assert.Equal("claude-opus-5-5", request.Step?.Model);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_model_leaves_the_requests_model_null()
    {
        Assert.Null(AgentLaunches.RequestFor(Run(), _target).Step?.Model);
        Assert.Null(AgentLaunches.RequestFor(Run(), _target, descriptor: null, new AgentLaunchExtras(Workspace: "/work")).Step?.Model);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_literal_model_in_the_recipe_is_read_as_the_asked_model()
    {
        var ask = AgentModels.Ask(Step(("model", new WithValue(JsonSerializer.SerializeToElement("claude-opus-5-5"), Fact: null, Profile: null))));

        Assert.Equal(new ModelAsk("claude-opus-5-5", Profile: null), ask);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_state_with_no_model_asks_for_none()
    {
        Assert.Equal(new ModelAsk(Literal: null, Profile: null), AgentModels.Ask(Step()));
        Assert.Equal(new ModelAsk(Literal: null, Profile: null), AgentModels.Ask(Step(("bundle", new WithValue(JsonSerializer.SerializeToElement("style"), Fact: null, Profile: null)))));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_profile_key_is_read_as_a_profile_ask_not_as_a_model()
    {
        var ask = AgentModels.Ask(Step(("model", new WithValue(Literal: null, Fact: null, Profile: "workflow.scoping.models.gapSearcher"))));

        Assert.Equal(new ModelAsk(Literal: null, Profile: "workflow.scoping.models.gapSearcher"), ask);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_blank_literal_asks_for_none() =>
        Assert.Equal(new ModelAsk(Literal: null, Profile: null), AgentModels.Ask(Step(("model", new WithValue(JsonSerializer.SerializeToElement("  "), Fact: null, Profile: null)))));

    [Fact, Trait("Lane", "unit")]
    public void The_refusal_of_a_profile_key_names_it_and_says_what_to_write_instead()
    {
        var message = AgentModels.Refusal("workflow.scoping.models.gapSearcher");

        Assert.Contains("workflow.scoping.models.gapSearcher", message, StringComparison.Ordinal);
        Assert.Contains("model id", message, StringComparison.Ordinal);
        Assert.Contains("fix:", message, StringComparison.Ordinal);
    }
}
