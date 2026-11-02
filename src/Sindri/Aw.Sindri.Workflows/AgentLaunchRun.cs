using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What 'LaunchAgentStep' is given (engine P8 U3): the run as the planner reads it, and the stored 'fact:item' that names the item and its repo.</summary>
/// <param name="Run">The run: the instance, the run token, the Step and whether it may write.</param>
/// <param name="Item">The instance's stored 'fact:item'; null when it has none.</param>
/// <param name="Workspace">The instance's stored 'fact:workspace' for a Step that launches in it (the CI-red autofix); null for every other Step.</param>
/// <param name="Bundle">The Step's 'bundle' parameter ('style' or 'lenses'), or null when it bundles no pack (LN-WF5).</param>
/// <param name="Subject">The Step's 'subject' parameter, which says which lens loop's pack a 'lenses' bundle holds; null for none.</param>
/// <param name="LensQueue">The instance's stored 'fact:lensQueue', whose head lens's prompt a 'lenses' bundle holds; null when it has none.</param>
/// <param name="Facts">The instance's stored facts; the launch writes those the Step declares as inputs into its run directory.</param>
/// <param name="Payloads">The payload facts the Step writes under its recipe (<see cref="PayloadFacts.OutputsOf"/>), whose shape its brief states; null for none.</param>
/// <param name="Model">The Step's 'model' parameter, a model id; null runs the agent's default.</param>
/// <param name="Previous">For an escalation: the run that failed (spec 5); null for a first run.</param>
/// <param name="Escalation">0 for the first run, 1 for its one retry on a stronger model (spec 7.1).</param>
/// <param name="Floor">The state's model floor ('cheap', 'standard' or 'strong'), or null for none (model-pick spec 8).</param>
/// <param name="Recipe">The recipe the instance runs, for the step's model history; null when unknown.</param>
/// <param name="RecipeVersion">The recipe's version.</param>
/// <param name="Agent">The state's 'agent:' (#1269): an agent id or a state to differ from; null for the profile's agent.</param>
/// <param name="ModelProfile">The profile key when the Step's 'model' is '{ profile: … }'; the launch refuses it (<see cref="AgentModels.Refusal"/>).</param>
internal sealed record AgentLaunchRun(AgentStepRun Run, FactValue? Item, FactValue? Workspace = null, string? Bundle = null, string? Subject = null, FactValue? LensQueue = null, IReadOnlyDictionary<FactKey, FactValue>? Facts = null, IReadOnlyList<FactKey>? Payloads = null, string? Model = null, string? ModelProfile = null, PreviousRun? Previous = null, int Escalation = 0, string? Floor = null, RecipeId? Recipe = null, int RecipeVersion = 0, StateAgent? Agent = null);
