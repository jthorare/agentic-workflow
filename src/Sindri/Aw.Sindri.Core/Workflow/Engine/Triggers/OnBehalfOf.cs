using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>A forwarded answer's origin (spec §5.8): the parent it was given to, and the actor who gave it, kept on the child's trigger.</summary>
/// <param name="Parent">The parent whose frame mirrors the child.</param>
/// <param name="Actor">The actor who answered the parent.</param>
public sealed record OnBehalfOf(WorkflowInstanceId Parent, string Actor);
