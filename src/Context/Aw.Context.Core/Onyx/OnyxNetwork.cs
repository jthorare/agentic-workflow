namespace Aw.Context.Core.Onyx;

/// <summary>Where the profile says Onyx is ('context.sources.onyx.network').</summary>
public enum OnyxNetwork
{
    /// <summary>On this machine: a loopback address (the default).</summary>
    Loopback,

    /// <summary>At a cluster address, such as a Service name (pod mode only; <see cref="OnyxClusterPolicy"/> decides).</summary>
    Cluster,
}
