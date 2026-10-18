using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>An agent Step run as the planner reads it (engine P8 D4, D8): which run, which Step, and whether its descriptor has a write scope.</summary>
/// <param name="Workflow">The workflow instance, whose id and the run token make the launch id.</param>
/// <param name="Address">The instance address the launch belongs to.</param>
/// <param name="Run">The run's token.</param>
/// <param name="Step">The recipe state and block whose seed prompt the session gets.</param>
/// <param name="WriteScope">Whether the Step's descriptor has <see cref="Blocks.Capability.WriteScope"/>.</param>
/// <param name="LoginSource">The model login source the Step's descriptor asks for, or null for no ask (the launcher takes the stricter of this and the profile's).</param>
public sealed record AgentStepRun(WorkflowInstanceId Workflow, InstanceAddress Address, RunToken Run, StepRef Step, bool WriteScope, ModelLoginSource? LoginSource = null);
