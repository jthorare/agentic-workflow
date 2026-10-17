using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxStatus;

internal sealed class OnyxStatusService(IOnyxPinsSource pinsSource, OnyxFacts facts, IOnyxHealth health, IOnyxWorkspace workspace)
    : IService<OnyxStatusRequest, OnyxStatusResponse>
{
    public async ValueTask<Result<OnyxStatusResponse>> Handle(OnyxStatusRequest request, CancellationToken cancellationToken)
    {
        var pins = pinsSource.Pins;
        var state = workspace.ReadState();
        var (docker, host) = facts.Read(cancellationToken);
        var compose = workspace.Read(facts.Paths.Compose);
        if (compose.IsError)
        {
            return compose.Error;
        }

        var setUp = IsSetUp(state, compose.Value);
        var (running, servingPort) = Serving(setUp, state, cancellationToken);
        var report = OnyxPreflight.Evaluate(docker, host, pins, running ? servingPort : null, facts.MemoryUsedByOthers(cancellationToken));
        var onyx = Classify(setUp, running, facts.DockerProblem(cancellationToken) is not null);
        var (url, version) = running ? await Running(report.Port, cancellationToken).ConfigureAwait(false) : (null, null);
        return new OnyxStatusResponse(onyx, pins.Version, pins.Commit, report.Checks, report.Port, report.Ok ? 0 : 1, url, version, Detail(onyx));
    }

    // Set up means `onyx up` finished its start step and the fetched files are still there.
    private static bool IsSetUp(OnyxSetupState? state, string? compose) => state is not null && state.Steps.ContainsKey(OnyxSteps.Start) && compose is not null;

    // Set up with a Docker that can't be asked: whether it runs is unknown, never a guess of stopped.
    private static OnyxState Classify(bool setUp, bool running, bool dockerUnusable)
        => !setUp ? OnyxState.NotSetUp : dockerUnusable ? OnyxState.Unknown : running ? OnyxState.Running : OnyxState.Stopped;

    // The address, and the release it reports (or unreachable when it doesn't answer).
    private async ValueTask<(string Url, string Version)> Running(int port, CancellationToken cancellationToken)
    {
        var probe = await health.Probe(port, cancellationToken).ConfigureAwait(false);
        return (string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}"), probe.Healthy ? probe.BackendVersion ?? "unreachable" : "unreachable");
    }

    private static string Detail(OnyxState state) => state switch
    {
        OnyxState.NotSetUp => "not set up: run `sindri onyx up`",
        OnyxState.Stopped => "stopped: run `sindri onyx up` to start it",
        OnyxState.Running => "running",
        OnyxState.Unknown => "unknown: Docker isn't usable here, so whether Onyx runs can't be told",
        _ => "",
    };

    // Whether Onyx is serving and the port it holds: the one nginx publishes (as in up and doctor), and state.json's only when no nginx publishes one; a state another home wrote is ignored.
    private (bool Running, int? Port) Serving(bool ask, OnyxSetupState? state, CancellationToken cancellationToken)
    {
        var containers = ask ? facts.Containers(cancellationToken) : null;
        if (containers is not { IsSuccess: true })
        {
            return (false, null);
        }

        var recorded = facts.ForeignProjectWarning(state) is null ? state?.Port : null;
        return (IsServing(containers.Value), OnyxFacts.OwnPort(containers.Value) ?? recorded);
    }

    // nginx and the API server both running is what "Onyx is up" means here; anything else is stopped. "Status" means "Onyx is serving" on purpose: down, doctor and
    // own-port use ComposeContainer.IsLive instead, because a restarting container is live (it holds its port and must be stopped) but not serving.
    private static bool IsServing(IReadOnlyList<ComposeContainer> containers)
        => containers.Any(static c => c.Service == "nginx" && c.State == "running")
            && containers.Any(static c => c.Service == "api_server" && c.State == "running");
}
