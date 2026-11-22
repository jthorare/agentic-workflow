using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Home;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Features.HomeLanes;

/// <summary>
/// The 'home-lanes' collector (home spec §3.2, §3.3, §8; E1): each minute it reads the box's aw-meta team, read-only, and raises and clears the 'lane-recycle',
/// 'agent-limited' and merge-queue 'pr-ready-merge' rows. When aw-meta can't be read (no team on this box) it writes nothing and keeps the rows it raised: the pass
/// is ok and its summary says 'lanes-unavailable', so a box without a team is not a failing service.
/// </summary>
/// <param name="team">Reads aw-meta.</param>
/// <param name="repos">Turns the team's repository directory into owner/name, so a queue row's id is the PR's.</param>
/// <param name="view">The Home rows this collector raised before.</param>
/// <param name="writer">Appends the pass's events.</param>
/// <param name="scrubber">Scrubs aw-meta's text before it reaches a row.</param>
/// <param name="clock">The read time each row's source carries.</param>
internal sealed class HomeLanesService(IMetaTeamReader team, IForgeRepos repos, IHomeViewReader view, HomeRowWriter writer, ITextScrubber scrubber, TimeProvider clock) : IScheduledService
{
    /// <summary>What the service does, as GET /sindri/services says it.</summary>
    public const string Description = "Puts lanes at their recycle mark, limited agents and the merge queue on Home, from aw-meta, read-only.";

    /// <summary>The summary of a pass that found no aw-meta team (the 'lanes-unavailable' code of home spec §10).</summary>
    internal const string Unavailable = "lanes-unavailable";

    internal static ServiceName ServiceId { get; } = ServiceName.From(HomeLaneRows.SourceName);

    /// <summary>Runs without a profile entry, every minute.</summary>
    internal static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(TimeSpan.FromMinutes(1)), Description);

    public ServiceName Identity() => ServiceId;

    public async ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var read = await team.Read(cancellationToken).ConfigureAwait(false);
        if (read.Unavailable is { } why)
        {
            return new ScheduledResult(Idle: true, $"ok: {Unavailable}: {scrubber.Scrub(why)}");
        }

        var held = await HomeHeldRows.Of(view, HomeLaneRows.SourceName, HomeLaneRows.Kinds, cancellationToken).ConfigureAwait(false);
        if (held.IsError)
        {
            return held.Error;
        }

        var repo = await RepoOf(read.RepoDirectory, cancellationToken).ConfigureAwait(false);
        var plan = HomeReconcile.Plan(HomeLaneRows.From(read, repo, clock.GetUtcNow(), scrubber), held.Value);
        if (plan.IsEmpty)
        {
            return ScheduledResult.Quiet;
        }

        var written = await writer.Write(plan, run, cancellationToken).ConfigureAwait(false);
        return written.IsError
            ? written.Error
            : new ScheduledResult(Idle: false, string.Create(CultureInfo.InvariantCulture, $"ok: {plan.Raised.Count} raised, {plan.Changed.Count} changed, {plan.Cleared.Count} cleared"));
    }

    // why: a repository the forge port can't name still gets queue rows, under 'lane:ready:<pr>' ids
    private async ValueTask<string?> RepoOf(string? directory, CancellationToken cancellationToken)
    {
        if (directory is null)
        {
            return null;
        }

        var repo = await repos.OfWorkspace(directory, cancellationToken).ConfigureAwait(false);
        return repo.IsError ? null : $"{repo.Value.Owner}/{repo.Value.Name}";
    }
}
