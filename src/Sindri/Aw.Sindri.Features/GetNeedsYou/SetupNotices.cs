using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.ApproveAll;

namespace Aw.Sindri.Features.GetNeedsYou;

/// <summary>
/// The setup rows the needs-you list adds to the views' (setup-once spec §7.3). 'setup:repo' stands while the profile names no repo and the last AddRepo job failed; it is cleared when a new AddRepo job runs
/// or succeeds. The sentence is the job's own, the fix in it. 'setup:recipes' and 'recipe:' come from the approve-all plan.
/// </summary>
/// <param name="jobs">The jobs view.</param>
/// <param name="live">The live profile: a repo in it means the step is done.</param>
/// <param name="plans">The approve-all plan, which the 'setup:recipes' and 'recipe:' rows are read from; a plan that can't be read drops those rows only.</param>
/// <param name="scrubber">Scrubs the text.</param>
/// <param name="clock">Stamps the recipe rows.</param>
/// <param name="mcp">Says whether the owner is to be asked once to turn the automatic registration on.</param>
internal sealed class SetupNotices(IJobViews jobs, IProfileSource live, ApproveAllPlanReader plans, ITextScrubber scrubber, TimeProvider clock, McpAutoGate mcp)
{
    private static readonly JobKind _addRepo = JobKind.From("AddRepo");
    private static readonly JobKind _registerMcp = JobKind.From("RegisterMcp");

    internal async ValueTask<NeedsYouRow?> Repo(CancellationToken cancellationToken)
    {
        if (await LastFailed(_addRepo, cancellationToken).ConfigureAwait(false) is not { } failed || await HasRepo(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var row = NeedsYouRows.ForSetupRepo(failed.Summary ?? "The last folder you picked could not be added. Pick the repo's top folder.", failed.EndedAt ?? failed.StartedAt, scrubber);
        return row.IsSuccess ? row.Value : null;
    }

    // 'setup:mcp' stands while the newest RegisterMcp job failed and none is running: the job's own sentence, the provider's fix in it. A new run clears it; a success replaces the failed job as the newest.
    internal async ValueTask<NeedsYouRow?> Mcp(CancellationToken cancellationToken)
    {
        if (await LastFailed(_registerMcp, cancellationToken).ConfigureAwait(false) is not { } failed)
        {
            var turnOn = await mcp.TurnOnNeeded(cancellationToken).ConfigureAwait(false) ? NeedsYouRows.ForTurnOnMcp(clock.GetUtcNow(), scrubber) : null;
            return turnOn is { IsSuccess: true } ? turnOn.Value : null;
        }

        var row = NeedsYouRows.ForSetupMcp(failed.Summary ?? "Sindri could not register itself with your agents. Press Connect again.", failed.EndedAt ?? failed.StartedAt, scrubber);
        return row.IsSuccess ? row.Value : null;
    }

    // The approve-all plan's rows (setup-once spec §7.3): read at request time from the registry and the approvals, and absent when the plan can't be read
    // (no approved profile yet, or the read failed): the list never fails for them.
    internal async ValueTask<IReadOnlyList<NeedsYouRow>> Recipes(CancellationToken cancellationToken)
    {
        try
        {
            var plan = await plans.Read(cancellationToken).ConfigureAwait(false);
            if (plan.IsError)
            {
                return [];
            }

            var rows = NeedsYouRows.ForRecipes(plan.Value, clock.GetUtcNow(), scrubber);
            return rows.IsSuccess ? rows.Value : [];
        }
#pragma warning disable CA1031 // boundary: a registry that throws drops these rows, never the list
        catch (Exception e) when (e is not OperationCanceledException)
#pragma warning restore CA1031
        {
            return [];
        }
    }

    // The newest job of a kind when it failed and none of that kind is running now; else null.
    private async ValueTask<JobRow?> LastFailed(JobKind kind, CancellationToken cancellationToken)
    {
        var running = await jobs.Running(cancellationToken).ConfigureAwait(false);
        var latest = await jobs.List(state: null, kind, cancellationToken).ConfigureAwait(false);
        if (running.IsError || latest.IsError || running.Value.Any(j => j.Kind == kind))
        {
            return null;
        }

        return latest.Value.Count > 0 && latest.Value[0].State == JobState.Failed ? latest.Value[0] : null;
    }

    private async ValueTask<bool> HasRepo(CancellationToken cancellationToken)
    {
        var profile = await live.Load(cancellationToken).ConfigureAwait(false);
        return profile.IsSuccess && profile.Value.Profile.Repos is { Count: > 0 };
    }
}
