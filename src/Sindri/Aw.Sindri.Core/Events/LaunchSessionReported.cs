using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'launch.session-reported' payload (launcher L-9): the agent's own session id, read by 'session wrap'. Key 'launch.session-reported:&lt;launch&gt;'.</summary>
/// <param name="Launch">The launch.</param>
/// <param name="SessionId">The id, bounded by the generic pattern and the entry's own.</param>
public sealed record LaunchSessionReported(LaunchId Launch, SessionId SessionId);
