using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// The approve-all routes of the fake (the plan, the app challenge, the approval and its job): each answers a golden, the same text the handler
/// serves. The engine's goldens are not on main yet, so the defaults are spec-shaped (Golden/SpecShaped/recipes-approve-all*, challenges-app,
/// approve-all-job; provisional, to be replaced by the engine's recipes-approve-all/*).
/// </summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<AppChallengeRequest> _challenges = [];
    private readonly List<ApproveAllRequest> _approvals = [];
    private readonly List<ApproveProfileRequest> _profileApprovals = [];
    private readonly List<RecipeApprovalSent> _recipeApprovals = [];
    private int _planCalls;
    private int _approveAllJobCalls;

    /// <summary>GET /sindri/workflow/recipes/approve-all's answer.</summary>
    public RouteAnswer ApproveAllPlanAnswer { get; set; } = RouteAnswer.Api("workflow-recipes-approve-all", "plan");

    /// <summary>POST /sindri/workflow/challenges/app's answer.</summary>
    public RouteAnswer AppChallengeAnswer { get; set; } = RouteAnswer.SpecShaped("challenges-app", "held");

    /// <summary>POST /sindri/workflow/recipes/approve-all's answer.</summary>
    public RouteAnswer ApproveAllStartAnswer { get; set; } = RouteAnswer.SpecShaped("recipes-approve-all-start", "started");

    /// <summary>POST /sindri/profile/approve's answer.</summary>
    public RouteAnswer ApproveProfileAnswer { get; set; } = RouteAnswer.Api("profile-approve", "approved");

    /// <summary>POST /sindri/workflow/recipes/{id}@{hash}/approve's answer.</summary>
    public RouteAnswer ApproveRecipeAnswer { get; set; } = RouteAnswer.SpecShaped("recipe-approve", "approved");

    /// <summary>Every recipe approval received, in order.</summary>
    public IReadOnlyList<RecipeApprovalSent> RecipeApprovals { get { lock (_gate) { return [.. _recipeApprovals]; } } }

    /// <summary>Held open until set: the profile approval waits for it (the app is sending meanwhile).</summary>
    public TaskCompletionSource? ApproveProfileGate { get; set; }

    /// <summary>Every profile approval body received, in order.</summary>
    public IReadOnlyList<ApproveProfileRequest> ProfileApprovals { get { lock (_gate) { return [.. _profileApprovals]; } } }

    /// <summary>What the job read answers once <see cref="ApproveAllJobAnswers"/> has none left to give.</summary>
    public RouteAnswer ApproveAllJobAnswer { get; set; } = RouteAnswer.SpecShaped("approve-all-job", "succeeded");

    /// <summary>The job answers handed out one per read, in order, before <see cref="ApproveAllJobAnswer"/> (a job that runs a while).</summary>
    public Queue<RouteAnswer> ApproveAllJobAnswers { get; } = new();

    /// <summary>Held open until set: the approval waits for it (the app is sending meanwhile).</summary>
    public TaskCompletionSource? ApproveAllGate { get; set; }

    /// <summary>Held open until set: the challenge request waits for it (a re-ask still on its way).</summary>
    public TaskCompletionSource? AppChallengeGate { get; set; }

    /// <summary>The answer the last job read gave (the handler serves the same text).</summary>
    public RouteAnswer LastJobAnswer { get; private set; } = RouteAnswer.SpecShaped("approve-all-job", "succeeded");

    /// <summary>Every challenge request received, in order.</summary>
    public IReadOnlyList<AppChallengeRequest> Challenges { get { lock (_gate) { return [.. _challenges]; } } }

    /// <summary>Every approval body received, in order.</summary>
    public IReadOnlyList<ApproveAllRequest> Approvals { get { lock (_gate) { return [.. _approvals]; } } }

    /// <summary>How many times the plan was read.</summary>
    public int PlanCalls => Volatile.Read(ref _planCalls);

    /// <summary>How many times the job was read.</summary>
    public int ApproveAllJobCalls => Volatile.Read(ref _approveAllJobCalls);

    public Task<Result<ApproveAllPlan>> ApproveAllPlan(Uri api, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _planCalls);
        return Task.FromResult(Answer<ApproveAllPlan>(api, WorkflowRoutes.ApproveAll, ApproveAllPlanAnswer));
    }

    public async Task<Result<AppChallengeView>> IssueAppChallenge(Uri api, AppChallengeRequest request, CancellationToken cancellationToken)
    {
        if (NotReceived<AppChallengeView>(api) is { } unanswered)
        {
            return unanswered;
        }

        lock (_gate)
        {
            _challenges.Add(request);
        }

        await WaitFor(AppChallengeGate, cancellationToken).ConfigureAwait(false);
        return Answer<AppChallengeView>(api, WorkflowRoutes.AppChallenge, AppChallengeAnswer);
    }

    public async Task<Result<ApproveAllStarted>> ApproveAll(Uri api, ApproveAllRequest request, CancellationToken cancellationToken)
    {
        if (NotReceived<ApproveAllStarted>(api) is { } unanswered)
        {
            return unanswered;
        }

        lock (_gate)
        {
            _approvals.Add(request);
        }

        await WaitFor(ApproveAllGate, cancellationToken).ConfigureAwait(false);
        return Answer<ApproveAllStarted>(api, WorkflowRoutes.ApproveAll, ApproveAllStartAnswer);
    }

    public Task<Result<ApproveAllJob>> ApproveAllJob(Uri api, string id, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _approveAllJobCalls);
        lock (_gate)
        {
            LastJobAnswer = ApproveAllJobAnswers.Count > 0 ? ApproveAllJobAnswers.Dequeue() : ApproveAllJobAnswer;
        }

        return Task.FromResult(Answer<ApproveAllJob>(api, WorkflowRoutes.Job(id), LastJobAnswer));
    }

    public async Task<Result<ProfileApproved>> ApproveProfile(Uri api, ApproveProfileRequest request, CancellationToken cancellationToken)
    {
        if (NotReceived<ProfileApproved>(api) is { } unanswered)
        {
            return unanswered;
        }

        lock (_gate)
        {
            _profileApprovals.Add(request);
        }

        await WaitFor(ApproveProfileGate, cancellationToken).ConfigureAwait(false);
        return Answer<ProfileApproved>(api, "/sindri/profile/approve", ApproveProfileAnswer);
    }

    public Task<Result<RecipeApproved>> ApproveRecipe(Uri api, string id, string hash, ProfileAnswer answer, CancellationToken cancellationToken)
    {
        if (NotReceived<RecipeApproved>(api) is { } unanswered)
        {
            return Task.FromResult(unanswered);
        }

        lock (_gate)
        {
            _recipeApprovals.Add(new RecipeApprovalSent(id, hash, answer));
        }

        return Task.FromResult(Answer<RecipeApproved>(api, WorkflowRoutes.ApproveRecipe(id, hash), ApproveRecipeAnswer));
    }
}
