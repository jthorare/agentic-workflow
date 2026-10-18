using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.SessionExit;
using Aw.Sindri.Features.SessionHeartbeat;
using Aw.Sindri.Host.Commands.SessionWrap;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Commands;

/// <summary>The wrap's calls over the CLI's control plane client: the heartbeat and exit routes, each with the launch's proof in its header.</summary>
/// <param name="client">The client.</param>
internal sealed class ControlPlaneWrapCalls(ControlPlaneClient client) : IWrapCalls
{
    public async ValueTask<Result<Unit>> Heartbeat(LaunchId launch, LaunchSecret proof, SessionId? session, CancellationToken cancellationToken)
    {
        var sent = await client.PostProven<SessionHeartbeatBody, SessionHeartbeatResponse>($"/sindri/sessions/{launch.Value}/heartbeat", new SessionHeartbeatBody(session), proof.Value, cancellationToken).ConfigureAwait(false);
        return sent.IsError ? sent.Error : Unit.Value;
    }

    public async ValueTask<Result<Unit>> Exit(LaunchId launch, LaunchSecret proof, int exitCode, SessionId? session, CancellationToken cancellationToken)
    {
        var sent = await client.PostProven<SessionExitBody, SessionExitResponse>($"/sindri/sessions/{launch.Value}/exit", new SessionExitBody(exitCode, session), proof.Value, cancellationToken).ConfigureAwait(false);
        return sent.IsError ? sent.Error : Unit.Value;
    }
}
