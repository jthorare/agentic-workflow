using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;


namespace Aw.Sindri.Features.RunService;

/// <summary>
/// What a RunService start checks inside the gate, so the HTTP caller sees the refusal at once (API P6 unit D): the service is one of the approved
/// profile's or a built-in (SND-SVC-404; a built-in runs by name though the profile does not name it, so the failure row's `sindri services run` works), this build has it (SND-SVC-404), and no scheduled run
/// of it is in flight (SND-JOB-409 without a running job). Two manual runs of one service are the registry's SND-JOB-409.
/// </summary>
/// <param name="approvals">The approved profile.</param>
/// <param name="scheduled">Every service this build has.</param>
/// <param name="builtIns">The services the build runs without a profile entry.</param>
/// <param name="guard">Says whether a run of the service is in flight.</param>
internal sealed class RunServiceAdmission(IApprovedProfile approvals, IEnumerable<IScheduledService> scheduled, IEnumerable<BuiltInService> builtIns, ServiceRunGuard guard) : IJobAdmission<RunServiceRequest>
{
    private bool _reserved;   // this instance is scoped to one start

    public void Rollback(RunServiceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_reserved)   // only what this start's own admission took: a refused second start must never give back the first one's claim
        {
            _reserved = false;
            guard.Unreserve(request.Name);
        }
    }

    public async ValueTask<Result<ServiceName?>> Admit(RunServiceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = request.Name;
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        if (!ServiceActions.CanRun(approved.Value?.Profile.Services?.ContainsKey(name.Value) == true, builtIns.Any(b => b.Name == name)))
        {
            return SindriErrors.ServiceNotFound.With($"The approved profile has no service '{name.Value}' (fix: sindri services)");
        }

        if (!scheduled.Any(s => s.Identity() == name))
        {
            return SindriErrors.ServiceNotFound.With($"The profile names service '{name.Value}', which this build doesn't have");
        }

        if (!guard.TryReserveManual(name))   // taken here, inside the gate, and handed to the work: no scheduled tick can claim after the 200
        {
            return guard.IsManualRun(name)
                ? (Result<ServiceName?>)name   // a manual run: the start refuses with SND-JOB-409 naming that job
                : RunServiceMessages.ScheduledInFlight(name);
        }

        _reserved = true;
        Result<ServiceName?> admitted = name;
        return admitted;
    }
}
