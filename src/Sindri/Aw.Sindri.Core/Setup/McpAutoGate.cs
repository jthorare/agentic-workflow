using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Setup;

/// <summary>
/// Whether Sindri may register itself in the repos on its own (setup-once spec §5.4): there is an approved profile, nothing turns it off (the profile key 'setup.mcp: off' or AW_SINDRI_APP_NO_MCP=1) and consent was recorded
/// for that profile (its approval on this build, or the owner's press of "Turn on for your repos"). It reads the approved snapshot, never the live file. A repo whose newest RegisterMcp job failed is left to the owner.
/// </summary>
/// <param name="approvals">The approved profile snapshot.</param>
/// <param name="facts">Reads the recorded consent.</param>
/// <param name="environment">The environment's off switch.</param>
/// <param name="jobs">The jobs view.</param>
public sealed class McpAutoGate(IApprovedProfile approvals, McpEvents facts, IMcpAutoSwitch environment, IJobViews jobs)
{
    private static readonly JobKind _kind = JobKind.From("RegisterMcp");

    /// <summary>The approved profile when the automatic runs may go ahead, else null.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The approved profile, or null.</returns>
    public async ValueTask<LoadedProfile?> Ready(CancellationToken cancellationToken) =>
        await Approved(cancellationToken).ConfigureAwait(false) is { } profile && await facts.ConsentGiven(profile.Hash, cancellationToken).ConfigureAwait(false) ? profile : null;

    /// <summary>Whether the owner is to be asked once: a profile naming repos was approved, nothing turns the automatic runs off, and no consent was recorded for it (it was approved before this build).</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>True when the "Turn on for your repos" row stands.</returns>
    public async ValueTask<bool> TurnOnNeeded(CancellationToken cancellationToken) =>
        await Approved(cancellationToken).ConfigureAwait(false) is { } profile && profile.Profile.Repos is { Count: > 0 } && !await facts.ConsentGiven(profile.Hash, cancellationToken).ConfigureAwait(false);

    /// <summary>Whether the newest RegisterMcp job for <paramref name="repo"/> failed: the owner is told by the needs-you row, and the automatic runs wait for the button.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True when the newest job of the repo failed.</returns>
    public async ValueTask<bool> RecentlyFailed(RepoName repo, CancellationToken cancellationToken)
    {
        var listed = await jobs.List(state: null, _kind, cancellationToken).ConfigureAwait(false);
        return listed.IsSuccess && listed.Value.FirstOrDefault(j => j.Repo == repo) is { State: JobState.Failed };
    }

    private async ValueTask<LoadedProfile?> Approved(CancellationToken cancellationToken)
    {
        if (environment.Off)
        {
            return null;
        }

        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        return approved is { IsSuccess: true, Value: { } profile } && !string.Equals(profile.Profile.Setup?.Mcp, SetupSettings.Off, StringComparison.Ordinal) ? profile : null;
    }
}
