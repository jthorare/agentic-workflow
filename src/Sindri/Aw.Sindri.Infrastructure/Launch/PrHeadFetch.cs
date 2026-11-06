using System.Globalization;
using Aw.Kernel;
using Aw.Redaction;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Workflow;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// For a skill Step with a 'pr' input (review, address-review): fetches the PR's head from origin into a branch of the run's own namespace, in the repo, before the session starts (skill flows spec §3.1). The session then reads the PR with
/// plain git, with no token and no network. A fetch that fails is the Step's failure, with git's words scrubbed, and no session starts.
/// </summary>
internal static class PrHeadFetch
{
    /// <summary>The environment variable that names the fetched branch for the session.</summary>
    internal const string BranchVariable = "AW_SINDRI_PR_REF";

    /// <summary>Fetches the PR head the request names.</summary>
    /// <param name="request">The launch.</param>
    /// <param name="cancellationToken">Cancels the fetch.</param>
    /// <returns>The branch and the sha it holds; null when the launch has no skill 'pr' input; SND-SKILL-006 or -008 when the input or the fetch is wrong.</returns>
    internal static async ValueTask<Result<FetchedPr?>> For(LaunchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request is not { Attended: false, Skill: { } skill, Instance: { } instance, Run: { } run } || !skill.Inputs.TryGetValue(PrReference.InputKey, out var input))
        {
            return Results.Success<FetchedPr?>(value: null);
        }

        var number = PrReference.Number(input);
        if (number.IsError)
        {
            return number.Error;
        }

        var branch = PrReference.BranchOf(WorkflowInstanceId.Of(instance), run.Number);
        var fetched = await GitProcess.Run(request.RepoPath, ["fetch", "--quiet", "origin", PrReference.Refspec(number.Value, branch)], cancellationToken).ConfigureAwait(false);
        if (fetched.Exit != 0)
        {
            return SindriErrors.SkillPrFetchFailed.With($"git fetch origin pull/{number.Value.ToString(CultureInfo.InvariantCulture)}/head failed: {Words(fetched)} (fix: check that the PR exists on origin and that origin is reachable)");
        }

        var head = await GitProcess.Run(request.RepoPath, ["rev-parse", "--verify", "refs/heads/" + branch], cancellationToken).ConfigureAwait(false);
        return head.Exit == 0 && head.Output.Length > 0
            ? new FetchedPr(branch, head.Output)
            : SindriErrors.SkillPrFetchFailed.With($"git fetched the PR but {branch} can't be read: {Words(head)}");
    }

    private static string Words(GitRun run) =>
        Redactor.Default.Scrub(run.Error.Trim().Length > 0 ? run.Error.Trim() : "exit " + run.Exit.ToString(CultureInfo.InvariantCulture)).Text;
}
