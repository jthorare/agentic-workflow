namespace Aw.Sindri.Core.Events;

/// <summary>Who this process acts as, for every event it records (factory addendum §17.21): set by the host, never by a caller.</summary>
/// <param name="Actor">The acting process, 'host:sindri'.</param>
/// <param name="Tenant">The tenant key (one value until team mode).</param>
/// <param name="Ring">The ring.</param>
public sealed record EventScope(string Actor, string Tenant, int Ring)
{
    /// <summary>The control plane's scope until team mode: actor host:sindri, tenant local, ring 0.</summary>
    public static EventScope ControlPlane { get; } = new("host:sindri", Profile.SindriProfile.DefaultTenant, 0);
}
