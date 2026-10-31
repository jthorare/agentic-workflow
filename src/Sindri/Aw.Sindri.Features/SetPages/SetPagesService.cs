using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.SetPages;

/// <summary>
/// The work of a SetPages job (pages spec §3.1): reads the live profile again (progress line 'checking'), works the request out and checks it against the map's facts, writes every edit once through <see cref="IProfileIndexWriter"/>
/// with the caller's profile hash (line 'written to the profile'), records 'context.page.written' with the pages and the actor (a failed record is a progress line, since the file already changed), and returns the new profile hash.
/// A dry run does everything up to the write and returns the rows that would change. Every refusal comes before the write. It never approves: the profile waits for the owner's approval as its own step.
/// </summary>
/// <param name="live">The live profile file.</param>
/// <param name="facts">What the current map says about pages.</param>
/// <param name="writer">Writes 'index.pages'.</param>
/// <param name="recorder">Records 'context.page.written'.</param>
/// <param name="jobs">The running job: its progress takes the lines.</param>
internal sealed class SetPagesService(IProfileSource live, IMapPageFacts facts, IProfileIndexWriter writer, PageEvents recorder, IJobContextAccessor jobs) : IService<SetPagesRequest, SetPagesResult>
{
    public async ValueTask<Result<SetPagesResult>> Handle(SetPagesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.DryRun && !recorder.CanRecord())
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");   // before anything is changed
        }

        jobs.Current?.Progress.Report("checking");
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error;
        }

        var map = await facts.Read(cancellationToken).ConfigureAwait(false);
        if (map.IsError)
        {
            return map.Error;
        }

        var plan = PagePlanner.Plan(request, loaded.Value.Profile, map.Value);   // a dry run is refused exactly as a write is
        if (plan.IsError)
        {
            return plan.Error;
        }

        if (request.DryRun)
        {
            return new SetPagesResult(plan.Value.Pages, loaded.Value.Hash, Written: false, Approved: false, plan.Value.Diff);
        }

        var written = await writer.Write(plan.Value.Edits, request.IfHash, cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            return written.Error;
        }

        jobs.Current?.Progress.Report("written to the profile");
        await Record(written.Value, plan.Value.Pages, cancellationToken).ConfigureAwait(false);
        return new SetPagesResult(plan.Value.Pages, written.Value, Written: true, Approved: false, plan.Value.Diff);
    }

    // The file has changed by now: a failed event is reported on the job, not returned, so the caller still gets the new hash and doesn't retry against the old one (SND-PROFILE-003).
    private async ValueTask Record(ProfileHash hash, IReadOnlyList<PageWritten> pages, CancellationToken cancellationToken)
    {
        var recorded = await recorder.Written(hash, [.. pages.Select(static p => $"{p.Id.Value}:{p.Action}")], cancellationToken).ConfigureAwait(false);
        if (recorded.IsError)
        {
            jobs.Current?.Progress.Report($"the page event wasn't recorded: {recorded.Error.Code}");
        }
    }
}
