namespace Aw.Sindri.Core.Resources;

/// <summary>Everything one owner declares.</summary>
/// <param name="Databases">The databases.</param>
/// <param name="Disks">The disks.</param>
/// <param name="Sandbox">The sandbox size, or null.</param>
/// <param name="Connections">The connections.</param>
public sealed record ResourceSpec(
    IReadOnlyList<DatabaseResource> Databases,
    IReadOnlyList<DiskResource> Disks,
    SandboxResource? Sandbox,
    IReadOnlyList<ConnectionResource> Connections)
{
    /// <summary>Gets a spec that declares nothing.</summary>
    public static ResourceSpec Empty { get; } = new([], [], null, []);
}
