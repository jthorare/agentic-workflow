using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'launch.viewer-attached' payload (launcher L-10): a client attached after 'launch.started' recorded 'unconfirmed'. Key 'launch.viewer-attached:&lt;launch&gt;'.</summary>
/// <param name="Launch">The launch.</param>
/// <param name="At">When the first client was seen.</param>
public sealed record LaunchViewerAttached(LaunchId Launch, DateTimeOffset At);
