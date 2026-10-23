using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxAccount;

/// <summary>Spec §4.1 steps 6-9 for the Onyx that `onyx up` just brought up, or that `onyx attach` reached at a cluster address: the owner's sign-up in the browser, Sindri's dedicated user and token, its storage and the profile section. Idempotent and resumable; the verb behind it is the human's.</summary>
/// <param name="Port">The loopback port Onyx answers on (the port of <paramref name="Cluster"/> when that is given).</param>
/// <param name="Version">The release the running Onyx reported.</param>
/// <param name="Cluster">The base URL of an Onyx at a cluster address, already checked against the environment; null for the managed stack on 127.0.0.1:<paramref name="Port"/>.</param>
public sealed record OnyxAccountRequest(int Port, string Version, Uri? Cluster = null)
{
    /// <summary>Gets where Onyx answers.</summary>
    public OnyxOrigin Origin => Cluster is { } url ? new OnyxOrigin(url) : OnyxOrigin.FromPort(Port);
}
