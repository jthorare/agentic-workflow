using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>A child instance as its parent's status shows it (engine spec rev 8 §5.8, §6.4 'child').</summary>
/// <param name="Address">The child's address.</param>
/// <param name="State">The state it is in.</param>
public sealed record ChildView(InstanceAddress Address, string State);
