using Aw.Kernel;

namespace Aw.Context.Core.Onyx;

/// <summary>
/// Whether an Onyx may be reached at a cluster address (deployable-context spec B.3). The environment is the authority, not the profile: only pod mode (AW_SINDRI_DEPLOY=pod)
/// and a host listed in AW_SINDRI_ONYX_HOSTS allow one, so an edit to the profile can't widen where Sindri connects. A loopback address is always allowed.
/// </summary>
public sealed class OnyxClusterPolicy
{
    private static readonly char[] _separators = [',', ' ', '\t', '\n', ';'];

    private OnyxClusterPolicy(bool podMode, IReadOnlySet<string> hosts)
    {
        PodMode = podMode;
        Hosts = hosts;
    }

    /// <summary>Gets the policy of a desktop: no cluster address.</summary>
    public static OnyxClusterPolicy Desktop { get; } = new(podMode: false, new HashSet<string>(StringComparer.Ordinal));

    /// <summary>Gets a value indicating whether this Sindri runs as the pod (AW_SINDRI_DEPLOY=pod).</summary>
    public bool PodMode { get; }

    /// <summary>Gets the host names (lower case, no port) that may be reached at a cluster address.</summary>
    public IReadOnlySet<string> Hosts { get; }

    /// <summary>Reads the two environment values.</summary>
    /// <param name="deploy">AW_SINDRI_DEPLOY; 'pod' (any case) selects pod mode.</param>
    /// <param name="hosts">AW_SINDRI_ONYX_HOSTS: host names separated by commas or spaces; null or empty lists none.</param>
    /// <returns>The policy.</returns>
    public static OnyxClusterPolicy From(string? deploy, string? hosts) => new(
        string.Equals(deploy, "pod", StringComparison.OrdinalIgnoreCase),
        new HashSet<string>((hosts ?? string.Empty).Split(_separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(static h => h.ToLowerInvariant()), StringComparer.Ordinal));

    /// <summary>Checks the profile's Onyx entry.</summary>
    /// <param name="settings">The checked settings.</param>
    /// <returns>Success, or SND-CONTEXT-055 naming why a cluster address is refused.</returns>
    public Result<Unit> Check(OnyxSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Network == OnyxNetwork.Loopback ? Unit.Value : CheckCluster(settings.Url);
    }

    /// <summary>Checks an address given on the command line: a loopback one is allowed, any other is a cluster address.</summary>
    /// <param name="url">The Onyx base URL.</param>
    /// <returns>Success, or SND-CONTEXT-055.</returns>
    public Result<Unit> Check(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        return OnyxSettings.IsLoopback(url) ? Unit.Value : CheckCluster(url);
    }

    private Result<Unit> CheckCluster(Uri url)
    {
        if (!PodMode)
        {
            return ContextErrors.OnyxNotPinnedOrLoopback.With(
                "context.sources.onyx.network: an Onyx at a cluster address is accepted only when this Sindri runs as the pod (AW_SINDRI_DEPLOY=pod); "
                + "here Onyx must be on a loopback address (spec B.3).");
        }

        return Hosts.Contains(url.Host.ToLowerInvariant())
            ? Unit.Value
            : ContextErrors.OnyxNotPinnedOrLoopback.With(
                $"context.sources.onyx.url: the host '{OnyxSettings.Shown(url.Host)}' isn't in AW_SINDRI_ONYX_HOSTS, the list of Onyx hosts this pod may reach (spec B.3); add the Onyx Service name to it.");
    }
}
