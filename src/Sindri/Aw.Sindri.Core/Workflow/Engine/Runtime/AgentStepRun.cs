using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>An agent Step run as the planner reads it (engine P8 D4, D8): which run, which Step, and whether its descriptor has a write scope.</summary>
/// <param name="Workflow">The workflow instance, whose id and the run token make the launch id.</param>
/// <param name="Address">The instance address the launch belongs to.</param>
/// <param name="Run">The run's token.</param>
/// <param name="Step">The recipe state and block whose seed prompt the session gets.</param>
/// <param name="WriteScope">Whether the Step's descriptor has <see cref="Blocks.Capability.WriteScope"/>.</param>
/// <param name="Escalation">0 for the first run of the entry, 1 for its one retry on a stronger model (model-pick spec 5, 7.1); the retry has its own launch id.</param>
public sealed record AgentStepRun(WorkflowInstanceId Workflow, InstanceAddress Address, RunToken Run, StepRef Step, bool WriteScope, int Escalation = 0);
