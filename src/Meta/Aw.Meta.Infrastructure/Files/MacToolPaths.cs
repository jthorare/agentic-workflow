namespace Aw.Meta.Infrastructure.Files;

/// <summary>
/// Where the macOS tools run from: always the absolute path macOS ships each at, so a PATH an agent or a stray directory controls
/// can't substitute another program. A tool missing at its path is never looked up by name: running it fails, and that failure is
/// AW-META-010 "unavailable on macOS" naming the tool (<see cref="MacTools"/>).
/// </summary>
/// <param name="Sysctl">The sysctl program.</param>
/// <param name="VmStat">The vm_stat program.</param>
/// <param name="Ps">The ps program.</param>
/// <param name="Kill">The kill program.</param>
/// <param name="Lsof">The lsof program.</param>
internal sealed record MacToolPaths(string Sysctl, string VmStat, string Ps, string Kill, string Lsof)
{
    /// <summary>The paths macOS ships the tools at.</summary>
    internal static MacToolPaths Default { get; } = new("/usr/sbin/sysctl", "/usr/bin/vm_stat", "/bin/ps", "/bin/kill", "/usr/sbin/lsof");
}
