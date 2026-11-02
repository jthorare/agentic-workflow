using System.Net;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>Decides whether a host and port may be reached, from the plan's static host groups.</summary>
/// <param name="groups">The plan's groups.</param>
/// <param name="resolver">Resolves names.</param>
/// <param name="mode">The plan's network mode.</param>
/// <param name="learnUntil">When learn mode stops; checked on every request.</param>
/// <param name="time">The clock.</param>
internal sealed class EgressPolicy(IReadOnlyList<PlanGroup> groups, IEgressResolver resolver, NetworkMode mode = NetworkMode.Enforce, DateTimeOffset? learnUntil = null, TimeProvider? time = null)
{
    private static readonly int[] _ports = [80, 443];

    // Loopback, link-local, private, shared, multicast, reserved and unspecified ranges.
    private static readonly List<IPNetwork> _notPublic = [.. new[]
    {
        "0.0.0.0/8", "10.0.0.0/8", "100.64.0.0/10", "127.0.0.0/8", "169.254.0.0/16", "172.16.0.0/12", "192.168.0.0/16", "224.0.0.0/3",
        "::/128", "::1/128", "fc00::/7", "fe80::/10", "ff00::/8",
    }.Select(IPNetwork.Parse)];

    /// <summary>Decides.</summary>
    /// <param name="host">The requested host name or address literal.</param>
    /// <param name="port">The requested port.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The decision.</returns>
    public async ValueTask<EgressDecision> Decide(string host, int port, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        var name = host.Trim().TrimEnd('.').Trim('[', ']');
        if (!_ports.Contains(port))
        {
            return new EgressDecision(EgressVerdict.Denied, Address: null);
        }

        var listed = HostGroups.Allowing(groups, name) is not null;
        if (!listed && !Learning())
        {
            return new EgressDecision(EgressVerdict.Denied, Address: null);
        }

        var addresses = IPAddress.TryParse(name, out var literal)
            ? [literal]
            : await resolver.Resolve(name, cancellationToken).ConfigureAwait(false);
        foreach (var address in addresses)
        {
            if (Reachable(address))
            {
                return new EgressDecision(listed ? EgressVerdict.Allowed : EgressVerdict.Learned, address);
            }
        }

        return new EgressDecision(EgressVerdict.Refused, Address: null);
    }

    private bool Learning() => mode == NetworkMode.Learn && learnUntil is { } until && (time ?? TimeProvider.System).GetUtcNow() < until;

    private bool Reachable(IPAddress address) => IsPublic(address) || HostGroups.Allowing(groups, address.ToString()) is not null;

    private static bool IsPublic(IPAddress address)
    {
        var plain = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        return !_notPublic.Exists(n => n.Contains(plain));
    }
}
