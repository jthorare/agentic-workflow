using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'sandbox.limit-hit' payload (sandbox spec §8). Key 'sandbox.limit-hit:&lt;launch&gt;:&lt;kind&gt;:&lt;n&gt;'.</summary>
/// <param name="Launch">The launch.</param>
/// <param name="Kind">Which limit.</param>
/// <param name="Peak">The peak: a count for pids, bytes for memory and disk.</param>
public sealed record SandboxLimitHit(LaunchId Launch, LimitHitKind Kind, long Peak);
