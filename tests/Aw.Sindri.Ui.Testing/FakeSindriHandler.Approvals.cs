using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The approve-all routes of <see cref="FakeSindriHandler"/>: the plan, the start, the app challenge and the job read (setup-once spec §8; provisional fixtures).</summary>
internal sealed partial class FakeSindriHandler
{
    private const string PlanPath = "/sindri/workflow/recipes/approve-all";
    private const string ChallengePath = "/sindri/workflow/challenges/app";
    private const string ProfileApprovePath = "/sindri/profile/approve";
    private const string JobsPrefix = "/sindri/jobs/";
    private const string ApproveAllJobId = "JOB-0000000000000000000000000000000e";   // the spec-shaped approve-all job; any other job goes to the Jobs routes

    private const string RecipesPrefix = "/sindri/workflow/recipes/";
    private const string ApproveSuffix = "/approve";

    private static bool IsRecipeApproval(HttpRequestMessage request, string path) =>
        request.Method == HttpMethod.Post && path.StartsWith(RecipesPrefix, StringComparison.Ordinal) && path.EndsWith(ApproveSuffix, StringComparison.Ordinal);

    private static bool IsApproval(HttpRequestMessage request, string path) =>
        IsRecipeApproval(request, path) || (string.Equals(path, ProfileApprovePath, StringComparison.Ordinal) && request.Method == HttpMethod.Post) || string.Equals(path, PlanPath, StringComparison.Ordinal) || (string.Equals(path, ChallengePath, StringComparison.Ordinal) && request.Method == HttpMethod.Post) || (request.Method == HttpMethod.Get && string.Equals(path, JobsPrefix + ApproveAllJobId, StringComparison.Ordinal));

    private async Task<HttpResponseMessage> ApprovalRoute(HttpRequestMessage request, string path, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!; // why: HttpClient always sets it on a sent request
        if (IsRecipeApproval(request, path))
        {
            // {id}@{hash}/approve, the hash escaped: the recipe's id never holds an '@'
            var named = Uri.UnescapeDataString(path[RecipesPrefix.Length..^ApproveSuffix.Length]).Split('@', 2);
            return Answer(Counted(await server.ApproveRecipe(uri, named[0], named[1], await Body<ProfileAnswer>(request, cancellationToken), cancellationToken), server.ApproveRecipeAnswer));
        }

        if (string.Equals(path, ProfileApprovePath, StringComparison.Ordinal))
        {
            return Answer(Counted(await server.ApproveProfile(uri, await Body<ApproveProfileRequest>(request, cancellationToken), cancellationToken), server.ApproveProfileAnswer));
        }

        if (string.Equals(path, ChallengePath, StringComparison.Ordinal))
        {
            return Answer(Counted(await server.IssueAppChallenge(uri, await Body<AppChallengeRequest>(request, cancellationToken), cancellationToken), server.AppChallengeAnswer));
        }

        if (string.Equals(path, PlanPath, StringComparison.Ordinal))
        {
            return request.Method == HttpMethod.Post
                ? Answer(Counted(await server.ApproveAll(uri, await Body<ApproveAllRequest>(request, cancellationToken), cancellationToken), server.ApproveAllStartAnswer))
                : Answer(Counted(await server.ApproveAllPlan(uri, cancellationToken), server.ApproveAllPlanAnswer));
        }

        return Answer(Counted(await server.ApproveAllJob(uri, Uri.UnescapeDataString(path[JobsPrefix.Length..]), cancellationToken), server.LastJobAnswer));
    }
}
