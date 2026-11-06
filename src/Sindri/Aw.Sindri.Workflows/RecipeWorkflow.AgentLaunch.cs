using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What an agent launch is given: the run, the item, the workspace, the Step's literals and payload facts, and the model it asks for.</summary>
internal sealed partial class RecipeWorkflow
{
    // What the launch activity is given: the run, the item, the workspace the Step launches in, what the Step's 'bundle' needs of the pack (LN-WF5): its 'subject' and the stored lens queue, and the model it asks for; the retry on a stronger model adds the run that failed.
    private AgentLaunchRun Launch(AgentStepRun run, FactValue? item, StepUse step, WorkflowInstance instance, FactValue? workspace)
    {
        var model = AgentModels.Ask(step);
        return new(run, item, workspace, Literal(step, "bundle"), Literal(step, "subject"), instance.Facts.TryGetValue(FactKey.From("fact:lensQueue"), out var queue) ? queue : null, instance.Facts, Payloads(step), model.Literal, model.Profile, Floor: FloorOf(run.Step.State), Agent: AgentOf(run.Step.State), Recipe: _recipe!.Version.Id, RecipeVersion: _recipe.Version.Version, Escalation: run.Escalation, SkillWith: SkillBlock(step) ? step.With : null, StepTimeout: SkillBlock(step) ? TimeoutOf(run.Step.State) : null);   // why: the loop runs only after RunAsync set _recipe
    }

    // A skill block's Step (skill flows spec §3.2): the block id is 'step.skill.<id>'.
    private static bool SkillBlock(StepUse step) => step.Use.Value.StartsWith("step.skill.", StringComparison.Ordinal);

    // The state's timeout, which a skill launch's wall limit is made from (skill flows spec D8); null when the state has none.
    private TimeSpan? TimeoutOf(string state) => _recipe!.Recipe.States.TryGetValue(state, out var found) ? found.Timeout?.After.Span : null;   // why: the loop runs only after RunAsync set _recipe

    // The state's model floor, or null when the state names none (model-pick spec 8).
    private StateAgent? AgentOf(string state) => _recipe!.Recipe.States.TryGetValue(state, out var found) ? found.Agent : null;   // why: the loop runs only after RunAsync set _recipe

    private string? FloorOf(string state) => _recipe!.Recipe.States.TryGetValue(state, out var found) ? found.Model?.Floor : null;   // why: the loop runs only after RunAsync set _recipe

    // The payload facts this Step writes (the recipe types them), so its brief states their shape.
    private IReadOnlyList<FactKey> Payloads(StepUse step) =>
        _recipe!.Catalog.Find(step.Use) is { } descriptor ? PayloadFacts.OutputsOf(_recipe.Recipe, descriptor) : [];   // why: the loop runs only after RunAsync set _recipe

    private static string? Literal(StepUse step, string name) =>
        step.With.TryGetValue(name, out var value) && value.Literal is { ValueKind: System.Text.Json.JsonValueKind.String } text ? text.GetString() : null;
}
