using System.Runtime.InteropServices;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>The OS the process runs on, as the host adapters see it.</summary>
/// <param name="Kind">Which adapters apply.</param>
/// <param name="Name">The OS's name, for messages.</param>
internal sealed record HostPlatform(HostKind Kind, string Name)
{
    /// <summary>Gets the platform this process runs on.</summary>
    internal static HostPlatform Current =>
        OperatingSystem.IsMacOS() ? new HostPlatform(HostKind.Mac, "macOS")
        : OperatingSystem.IsLinux() ? new HostPlatform(HostKind.Linux, "Linux")
        : new HostPlatform(HostKind.Unsupported, RuntimeInformation.OSDescription);
}
