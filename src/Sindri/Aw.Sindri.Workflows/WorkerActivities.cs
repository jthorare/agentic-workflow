namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The activity classes the worker serves next to <see cref="RecipeWorkflow"/> (engine spec rev 8 §0): the interpreter's own, the event mirror, the fact readers, the effects, (engine P7) the gates and the claim cleanup and (engine P8) the agent runtime's.</summary>
/// <param name="Steps">RunStep, RunBlockStep and LoadRecipe.</param>
/// <param name="Events">RecordEvent (the interpreter runs it as a local activity).</param>
/// <param name="Items">ReadItemRepo and ReadSubject (local activities on start).</param>
/// <param name="Effects">RecordEffect, RecheckEffect and RunEffect (engine P6).</param>
/// <param name="Engine">EvaluateGates and ReleaseClaim (engine P7).</param>
/// <param name="Inputs">The Step input activity: FillInput, and the local DropInjection.</param>
/// <param name="Agents">LaunchAgentStep, WatchAgentStep and CloseAgentStep (engine P8).</param>
/// <param name="Graph">The graph workflow's activities (issue 2258, PR 6).</param>
internal sealed record WorkerActivities(RunStepActivities Steps, RecordEventActivities Events, FactReadActivities Items, EffectActivities Effects, EngineActivities Engine, StepInputActivities Inputs, AgentStepActivities Agents, GraphActivities Graph);
