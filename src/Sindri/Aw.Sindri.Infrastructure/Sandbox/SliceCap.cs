using System.Globalization;

namespace Aw.Sindri.Infrastructure.Sandbox;

#pragma warning disable MA0182 // until the launcher (SB-5a) applies it
/// <summary>Caps the memory of all sandboxed sessions together (sandbox spec §3.3). Pure.</summary>
internal static class SliceCap
{
    /// <summary>Builds the systemctl command that puts the total on the slice.</summary>
    /// <param name="systemctl">The systemctl program.</param>
    /// <param name="limits">The limits.</param>
    /// <returns>The argv.</returns>
    // The unit is sindri-steps.slice itself: the dash in its name only places it under sindri.slice in the cgroup tree (spike item 4),
    // and every scope is started with --slice=sindri-steps.slice, so this one unit holds all of them. Setting it on sindri.slice would cap whatever else lives there.
    // --runtime: the cap lasts until the user manager restarts, and the launcher sets it again before each launch.
    internal static IReadOnlyList<string> Argv(string systemctl, ScopeLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        return [systemctl, "--user", "set-property", "--runtime", SystemdScopeArgv.Slice, $"MemoryMax={limits.SliceTotalBytes.ToString(CultureInfo.InvariantCulture)}"];
    }
}
#pragma warning restore MA0182
