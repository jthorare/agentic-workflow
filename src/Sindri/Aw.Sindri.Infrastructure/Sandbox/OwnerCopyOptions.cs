namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>Where the owner-copy source reads the owner's login and writes the per-launch copies (sandbox spec §3.1, §3.4).</summary>
/// <param name="Home">The owner's home directory.</param>
/// <param name="SindriHome">AW_SINDRI_HOME: the copies live under 'launches/&lt;launch&gt;/login'.</param>
internal sealed record OwnerCopyOptions(string Home, string SindriHome)
{
    /// <summary>How old a copy must be before the crash sweep may remove it: a launch that is still preparing has a copy and no scope yet.</summary>
    internal static readonly TimeSpan SweepGrace = TimeSpan.FromMinutes(2);

    /// <summary>Codex refreshes a login older than this when it starts (spike item 1), which would rotate the owner's refresh token.</summary>
    internal static readonly TimeSpan CodexRefreshAge = TimeSpan.FromDays(8);
}
