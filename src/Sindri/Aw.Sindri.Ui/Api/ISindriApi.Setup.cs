using Aw.Kernel;
using Aw.Sindri.Ui.Api.Setup;

namespace Aw.Sindri.Ui.Api;

/// <summary>The setup read (<see cref="ISindriApi"/>, continued; setup-once spec §7): where "set up Sindri once" stands.</summary>
internal partial interface ISindriApi
{
    /// <summary>GET /sindri/setup: the six steps with their states. A pure read: it starts nothing. A source that can't be read fails only its own step.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The steps, the API's refusal (a 404 from a Sindri before setup), or SND-UI-002/003.</returns>
    public Task<Result<SetupView>> Setup(Uri api, CancellationToken cancellationToken);

    /// <summary>POST /sindri/setup/repos: adds a repo by the folder's path (AddRepo, a job). Sent once; the cheap checks answer SND-SETUP-001 or SND-SETUP-002 before a job starts.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="request">The folder.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The started job, SND-SETUP-001/002, SND-AUTH-002 for an agent session, or SND-UI-002/003.</returns>
    public Task<Result<JobStarted>> AddRepo(Uri api, AddRepoRequest request, CancellationToken cancellationToken);

    /// <summary>POST /sindri/setup/mcp: registers Sindri in the approved repos for the coding agents found on the box (RegisterMcp).</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="request">The repo, or none for all.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The started job, SND-SETUP-004, SND-AUTH-002 for an agent session, or SND-UI-002/003.</returns>
    public Task<Result<JobStarted>> RegisterMcp(Uri api, RegisterMcpRequest request, CancellationToken cancellationToken);

}
