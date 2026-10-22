using Aw.Kernel;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Api;

/// <summary>The approve-all routes (<see cref="ISindriApi"/>, continued): the plan, the app challenge, the approval and the job it runs as.</summary>
internal partial interface ISindriApi
{
    /// <summary>GET /sindri/workflow/recipes/approve-all: the plan one approval covers (ordered, a reason each, a digest).</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The plan, the API's refusal (SND-SETUP-007 before a profile is approved), or SND-UI-002/003.</returns>
    public Task<Result<ApproveAllPlan>> ApproveAllPlan(Uri api, CancellationToken cancellationToken);

    /// <summary>POST /sindri/workflow/challenges/app: the challenge for 'approve-all &lt;digest6&gt;'. A human token only; sent once.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="request">The action text.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The challenge, SND-AUTH-002 for an agent session, SND-SETUP-007, or SND-UI-002/003.</returns>
    public Task<Result<AppChallengeView>> IssueAppChallenge(Uri api, AppChallengeRequest request, CancellationToken cancellationToken);

    /// <summary>POST /sindri/workflow/recipes/approve-all: redeems the challenge and starts the approval job. Sent once, never retried by the app.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="request">The challenge and the answer (held or typed).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The started job, SND-SETUP-003 (hold again), SND-RECIPE-009 (the plan changed), SND-JOB-409, SND-AUTH-002, or SND-UI-002/003.</returns>
    public Task<Result<ApproveAllStarted>> ApproveAll(Uri api, ApproveAllRequest request, CancellationToken cancellationToken);

    /// <summary>GET /sindri/jobs/{id}: how far the approval job is and what it did.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="id">The job.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The job, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<ApproveAllJob>> ApproveAllJob(Uri api, string id, CancellationToken cancellationToken);

    /// <summary>POST /sindri/profile/approve: approves the live profile by redeeming its typed challenge. A human token only; sent once, never retried by the app.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="request">The full hash and the answer.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The approval, SND-AUTH-002 for an agent session, the API's other refusals (a stale hash, a wrong answer), or SND-UI-002/003.</returns>
    public Task<Result<ProfileApproved>> ApproveProfile(Uri api, ApproveProfileRequest request, CancellationToken cancellationToken);

    /// <summary>POST /sindri/workflow/recipes/{id}@{hash}/approve: approves one changed workflow by redeeming its typed challenge. A human token only; sent once, never retried by the app.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="id">The recipe's id.</param>
    /// <param name="hash">The hash to approve, as the recipe list serves it.</param>
    /// <param name="answer">The challenge and the text the person typed.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The accepted approval, SND-AUTH-002 for an agent session, the API's other refusals, or SND-UI-002/003.</returns>
    public Task<Result<RecipeApproved>> ApproveRecipe(Uri api, string id, string hash, ProfileAnswer answer, CancellationToken cancellationToken);
}
