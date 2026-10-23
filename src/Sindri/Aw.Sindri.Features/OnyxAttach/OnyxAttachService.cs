using Aw.Context.Core.Onyx;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxAttach;

/// <summary>
/// `onyx up` with the stack stage replaced (deployable-context spec C.3): the address must be one the environment allows (pod mode and a host in AW_SINDRI_ONYX_HOSTS), the Onyx there must answer its health route and
/// report the release this build pins; then the same account stage as `onyx up` (the owner's sign-up, Sindri's dedicated user and token, the profile section with `network: cluster`) runs against it. Nothing is started, pulled or written to Onyx's workspace.
/// </summary>
/// <param name="policy">The environment's rule for an Onyx at a cluster address.</param>
/// <param name="health">Asks the Onyx whether it is ready, and which release it runs.</param>
/// <param name="pinsSource">The pins this build was made with.</param>
/// <param name="account">The account stage (steps 6-9) against the origin.</param>
/// <param name="progress">Where progress lines go.</param>
internal sealed class OnyxAttachService(OnyxClusterPolicy policy, IOnyxHealth health, IOnyxPinsSource pinsSource, IOnyxAccountStage account, IOnyxProgress progress)
    : IService<OnyxAttachRequest, OnyxAccountStageResult>
{
    public async ValueTask<Result<OnyxAccountStageResult>> Handle(OnyxAttachRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var url = Parse(request.Url);
        if (url.IsError)
        {
            return url.Error;
        }

        var allowed = policy.Check(url.Value);
        if (allowed.IsError)
        {
            return allowed.Error;
        }

        var origin = new OnyxOrigin(url.Value);
        var version = await Reachable(origin, pinsSource.Pins, cancellationToken).ConfigureAwait(false);
        if (version.IsError)
        {
            return version.Error;
        }

        progress.Line($"attach: Onyx {version.Value} answers at {origin} (the pinned release)");
        return await account.Run(origin, version.Value, cancellationToken).ConfigureAwait(false);
    }

    // The base URL only: http or https, a host, no user, path, query or fragment. A loopback address is `onyx up`'s (the managed stack), not an attached one.
    private static Result<Uri> Parse(string text)
    {
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return SindriErrors.InvalidInput.With("`sindri onyx attach` takes Onyx's base URL, such as http://onyx-api-service.sindri.svc:8080 (fix: give an absolute http or https URL)");
        }

        if (uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.AbsolutePath != "/")
        {
            return SindriErrors.InvalidInput.With("`sindri onyx attach` takes the base URL only: no user name or password, path, query or fragment (fix: give scheme, host and port)");
        }

        return OnyxSettings.IsLoopback(uri)
            ? SindriErrors.InvalidInput.With("`sindri onyx attach` is for an Onyx at a cluster address; the Onyx on this machine is `sindri onyx up`'s (fix: run `sindri onyx up`)")
            : uri;
    }

    private async ValueTask<Result<string>> Reachable(OnyxOrigin origin, OnyxPins pins, CancellationToken cancellationToken)
    {
        var probe = await health.Probe(origin, cancellationToken).ConfigureAwait(false);
        if (!probe.Healthy)
        {
            return SindriErrors.OnyxNotReady.With($"Onyx didn't answer /api/health at {origin}; check that the Onyx release is running and that this address is reachable from here, then run `sindri onyx attach {origin}` again");
        }

        if (probe.BackendVersion is not { } version)
        {
            return SindriErrors.OnyxNotReady.With($"{origin} answered /api/health but its version couldn't be read; is it Onyx? Run `sindri onyx attach {origin}` again once it is up");
        }

        return pins.IsPinnedRelease(version)
            ? version
            : SindriErrors.OnyxNotReady.With($"Onyx at {origin} runs version {version} but this Sindri is pinned to {pins.Version}; run the pinned release there (context/onyx.lock), then attach again");
    }
}
