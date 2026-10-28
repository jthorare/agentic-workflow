using Aw.Kernel;
using Aw.Sindri.Ui.Api.Sandbox;

namespace Aw.Sindri.Ui.Api;

/// <summary>The sandbox's reads and its one write (<see cref="ISindriApi"/>, continued; SB-6).</summary>
internal partial interface ISindriApi
{
    /// <summary>GET /sindri/sandbox/status: the last check's result plus the config. Cheap, never probes, never carries X-Sindri-After.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The status (CheckedAt null when never checked), or the refusal.</returns>
    public Task<Result<SandboxStatusView>> SandboxStatus(Uri api, CancellationToken cancellationToken);

    /// <summary>GET /sindri/sandbox/check: Check again. Read-only on the server: it resumes nothing.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Every blocker and the Steps a resume would start, or the refusal.</returns>
    public Task<Result<SandboxCheckView>> SandboxCheck(Uri api, CancellationToken cancellationToken);

    /// <summary>GET /sindri/sandbox/denials?item=: the denial counters of the item's launches.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="item">The item id (ITM-…).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One counter per launch, newest first, or the refusal.</returns>
    public Task<Result<DenialsView>> SandboxDenials(Uri api, string item, CancellationToken cancellationToken);

    /// <summary>POST /sindri/sandbox/allow: a draft profile edit that opens the profile approval. Human only; sent once, the app never retries on its own.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="request">The launch, kind and target.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>'needs-approval' with the approval id, SND-AUTH-002 for a session, or a SND-SANDBOX refusal for a permanent path or a policy kind.</returns>
    public Task<Result<AllowView>> SandboxAllow(Uri api, AllowRequest request, CancellationToken cancellationToken);
}
