using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetNeedsYou;

/// <summary>
/// The setup rows the needs-you list adds to the views' (setup-once spec §7.3). 'setup:repo' stands while the profile names no repo and the last AddRepo job failed; it is cleared when a new AddRepo job runs
/// or succeeds. The sentence is the job's own, the fix in it.
/// </summary>
/// <param name="jobs">The jobs view.</param>
/// <param name="live">The live profile: a repo in it means the step is done.</param>
/// <param name="scrubber">Scrubs the text.</param>
/// <param name="clock">Stamps the rows.</param>
/// <param name="mcp">Says whether the owner is to be asked once to turn the automatic registration on.</param>
/// <param name="sighting">Keeps when the turn-on row was first seen.</param>
internal sealed class SetupNotices(IJobViews jobs, IProfileSource live, ITextScrubber scrubber, TimeProvider clock, McpAutoGate mcp, TurnOnMcpSighting sighting)
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
            if (!await mcp.TurnOnNeeded(cancellationToken).ConfigureAwait(false))
            {
                sighting.Clear();
                return null;
            }

            var turnOn = NeedsYouRows.ForTurnOnMcp(sighting.Seen(clock.GetUtcNow()), scrubber);
            return turnOn is { IsSuccess: true } ? turnOn.Value : null;
        }

        sighting.Clear();
        var row = NeedsYouRows.ForSetupMcp(failed.Summary ?? "Sindri could not register itself with your agents. Press Connect again.", failed.EndedAt ?? failed.StartedAt, scrubber);
        return row.IsSuccess ? row.Value : null;
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
