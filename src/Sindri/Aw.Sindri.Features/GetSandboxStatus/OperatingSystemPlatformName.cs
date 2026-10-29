namespace Aw.Sindri.Features.GetSandboxStatus;

/// <summary>Linux runs bubblewrap, macOS Seatbelt; any other system has no mechanism.</summary>
internal sealed class OperatingSystemPlatformName : ISandboxPlatformName
{
    public string Mechanism => OperatingSystem.IsLinux() ? "bubblewrap" : OperatingSystem.IsMacOS() ? "seatbelt" : "none";
}
