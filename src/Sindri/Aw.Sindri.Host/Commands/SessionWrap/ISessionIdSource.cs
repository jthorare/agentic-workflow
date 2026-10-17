using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>Where 'session wrap' looks for the agent's own session id (launcher L-9).</summary>
internal interface ISessionIdSource
{
    /// <summary>Looks once.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="startedAt">When the agent started.</param>
    /// <param name="elapsed">How long wrap has waited: the file fallback only runs once the hook has had its chance.</param>
    /// <param name="cancellationToken">Cancels the look.</param>
    /// <returns>The id, ambiguity or nothing yet.</returns>
    public ValueTask<SessionIdScan> Look(LaunchId launch, DateTimeOffset startedAt, TimeSpan elapsed, CancellationToken cancellationToken);
}
