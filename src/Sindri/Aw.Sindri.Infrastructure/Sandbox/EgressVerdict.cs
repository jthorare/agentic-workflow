namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>What the egress proxy decided about one request (sandbox spec §3.2).</summary>
internal enum EgressVerdict
{
    /// <summary>A group allows the host and the target address is public (or listed).</summary>
    Allowed,

    /// <summary>Learn mode let a host through that no group lists; the public address is dialed like an allowed one and the log lists the host to add.</summary>
    Learned,

    /// <summary>No group allows the host or port.</summary>
    Denied,

    /// <summary>The host is allowed by name but resolves to a loopback, link-local or private address, or a cap was hit.</summary>
    Refused,

    /// <summary>A group allowed the host but the dial failed.</summary>
    Failed,
}
