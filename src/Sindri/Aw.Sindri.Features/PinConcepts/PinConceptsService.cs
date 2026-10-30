using Aw.Context.Core;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.PinConcepts;

/// <summary>
/// The work of a PinConcepts job (concept pins spec §3.6): reads the live profile again (progress line 'checking'), works the request out and checks it with the profile's rules, checks the feature keys against the current map,
/// writes every edit once through <see cref="IProfileConceptWriter"/> with the caller's profile hash (line 'written to the profile'), records 'context.pin.written' with the pins and the actor, and returns the new profile
/// hash. Every refusal comes before the write. It never approves: the profile waits for the owner's approval as its own step.
/// </summary>
/// <param name="live">The live profile file.</param>
/// <param name="features">Which feature keys the current map holds.</param>
/// <param name="writer">Writes 'index.concepts'.</param>
/// <param name="recorder">Records 'context.pin.written'.</param>
/// <param name="jobs">The running job: its progress takes the lines.</param>
internal sealed class PinConceptsService(IProfileSource live, IMapFeatures features, IProfileConceptWriter writer, PinEvents recorder, IJobContextAccessor jobs) : IService<PinConceptsRequest, PinConceptsResult>
{
    public async ValueTask<Result<PinConceptsResult>> Handle(PinConceptsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!recorder.CanRecord())
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");   // before anything is changed
        }

        jobs.Current?.Progress.Report("checking");
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error;
        }

        var plan = PinPlanner.Plan(request, loaded.Value.Profile);
        if (plan.IsError)
        {
            return plan.Error;
        }

        var unknown = await UnknownFeature(plan.Value.Features, cancellationToken).ConfigureAwait(false);
        if (unknown is not null)
        {
            return unknown;
        }

        var written = await writer.Write(plan.Value.Edits, request.IfHash, cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            return written.Error;
        }

        jobs.Current?.Progress.Report("written to the profile");
        var recorded = await recorder.Written(written.Value, [.. plan.Value.Pins.Select(static p => $"{p.Id.Value}:{p.Action}")], cancellationToken).ConfigureAwait(false);
        return recorded.IsError ? recorded.Error : new PinConceptsResult(plan.Value.Pins, written.Value, Approved: false, plan.Value.Dropped);
    }

    // No feature keys, no map read: a pin without features pins before the first map is built. With keys and no map the pin is refused, since nothing could be checked.
    private async ValueTask<Error?> UnknownFeature(IReadOnlyList<string> keys, CancellationToken cancellationToken)
    {
        if (keys.Count == 0)
        {
            return null;
        }

        var check = await features.Check(keys, cancellationToken).ConfigureAwait(false);
        if (check.IsError)
        {
            return check.Error;
        }

        if (check.Value.Missing.Count == 0)
        {
            return null;
        }

        return check.Value.Version is null
            ? ContextErrors.PinFeatureUnknown.With($"{check.Value.Missing[0]} can't be checked: no map is built yet (fix: build the map first, or pin without features)")
            : ContextErrors.PinFeatureUnknown.With($"{check.Value.Missing[0]} is not in map {check.Value.Version} (fix: pick it from context_concepts or the feature picker)");
    }
}
