using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxStatus;

internal sealed class OnyxStatusService(IOnyxPinsSource pinsSource, IContainerRuntime runtime, IHostProbes probes, ISindriHome home)
    : IService<OnyxStatusRequest, OnyxStatusResponse>
{
    public ValueTask<Result<OnyxStatusResponse>> Handle(OnyxStatusRequest request, CancellationToken cancellationToken)
    {
        var pins = pinsSource.Pins;
        var docker = runtime.Inspect(cancellationToken);
        var host = new HostFacts(
            probes.FreeDisk(home.Path),
            docker is { DataInVm: false, DataRoot: { } dataRoot } ? probes.FreeDiskIfExists(dataRoot) : null,
            probes.Ports(OnyxPreflight.PreferredPort, OnyxPreflight.FallbackFirst, OnyxPreflight.FallbackLast));
        var report = OnyxPreflight.Evaluate(docker, host, pins);
        Result<OnyxStatusResponse> response = new OnyxStatusResponse(OnyxState.NotSetUp, pins.Version, pins.Commit, report.Checks, report.Port, report.Ok ? 0 : 1);
        return ValueTask.FromResult(response);
    }
}
