using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>One piece of work the interpreter does once the decision is in history (spec §5.3).</summary>
/// <param name="Kind">What to do.</param>
/// <param name="Instance">The instance it is for (the child, for <see cref="FollowUpKind.CancelChild"/>).</param>
/// <param name="Run">The run to start, for <see cref="FollowUpKind.StartRun"/>.</param>
/// <param name="Step">The Step to start, for <see cref="FollowUpKind.StartRun"/>.</param>
/// <param name="Answer">The answer's key the re-evaluation runs under, for <see cref="FollowUpKind.Reevaluate"/>.</param>
public sealed record FollowUp(FollowUpKind Kind, WorkflowInstanceId Instance, RunToken? Run, BlockId? Step, TriggerKey? Answer);
