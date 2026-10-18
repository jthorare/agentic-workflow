using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What 'LaunchAgentStep' is given (engine P8 U3): the run as the planner reads it, and the stored 'fact:item' that names the item and its repo.</summary>
/// <param name="Run">The run: the instance, the run token, the Step and whether it may write.</param>
/// <param name="Item">The instance's stored 'fact:item'; null when it has none.</param>
/// <param name="Inputs">The facts the Step's descriptor declares as inputs and the instance has, by key; they are written into the run folder before the launch.</param>
internal sealed record AgentLaunchRun(AgentStepRun Run, FactValue? Item, IReadOnlyDictionary<string, FactValue>? Inputs = null);
