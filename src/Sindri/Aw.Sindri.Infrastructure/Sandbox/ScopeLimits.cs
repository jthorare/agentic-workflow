using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// The numbers one sandboxed session's cgroup scope is started with (sandbox spec §3.3). The profile's values win; a key left out takes
/// <see cref="SandboxDefaults"/>.
/// </summary>
/// <param name="MemoryMaxBytes">The kill line: 'auto' is a fifth of RAM, at most 12 GiB.</param>
/// <param name="MemoryHighBytes">The throttle line: 80% of the max.</param>
/// <param name="SliceTotalBytes">The parent slice's memory cap for all sessions together.</param>
/// <param name="CpuQuotaPercent">The CPU quota in percent of one core (all cores at the per-core share).</param>
/// <param name="CpuWeight">The CPU weight against other work.</param>
/// <param name="TasksMax">Processes per session.</param>
/// <param name="Wall">The unsuspended run time allowed, grace included; null for an attended session (no wall).</param>
/// <param name="WallGrace">The grace already inside <paramref name="Wall"/>.</param>
/// <param name="ParkMax">The longest single suspension the scope's loose ceiling allows for.</param>
internal sealed partial record ScopeLimits(long MemoryMaxBytes, long MemoryHighBytes, long SliceTotalBytes, int CpuQuotaPercent, int CpuWeight, int TasksMax,
    TimeSpan? Wall, TimeSpan WallGrace, TimeSpan ParkMax)
{
    internal const long Gib = 1024L * 1024 * 1024;
    private static readonly TimeSpan _noTimeoutWall = TimeSpan.FromHours(4);

    /// <summary>Resolves the limits for one launch.</summary>
    /// <param name="profile">The profile's 'sandbox.limits', or null.</param>
    /// <param name="box">The box's RAM and cores.</param>
    /// <param name="stepTimeout">The Step's timeout, or null when it has none (4 h is then the wall before the grace).</param>
    /// <param name="longMax">The longest wall any Step gets before the grace.</param>
    /// <param name="attended">True for a session the owner opened: no wall.</param>
    /// <returns>The limits.</returns>
    internal static ScopeLimits From(SandboxLimits? profile, BoxFacts box, TimeSpan? stepTimeout, TimeSpan longMax, bool attended)
    {
        ArgumentNullException.ThrowIfNull(box);
        var p = profile ?? new SandboxLimits();
        var max = MemoryMax(Or(p.Memory, SandboxDefaults.Memory), box.RamBytes);
        var grace = Duration(Or(p.WallGrace, SandboxDefaults.WallGrace));
        return new ScopeLimits(
            max,
            max * 4 / 5,
            Total(Or(p.Total, SandboxDefaults.Total), box.RamBytes),
            Math.Max(1, box.Cores) * Percent(Or(p.Cpu, SandboxDefaults.Cpu)),
            50,
            p.Tasks ?? SandboxDefaults.Tasks,
            WallFor(stepTimeout, longMax, attended, grace),
            grace,
            Duration(Or(p.ParkMax, SandboxDefaults.ParkMax)));
    }

    private static string Or(string? value, string fallback) => value ?? fallback;

    private static TimeSpan? WallFor(TimeSpan? stepTimeout, TimeSpan longMax, bool attended, TimeSpan grace) =>
        attended ? null : Min(stepTimeout ?? _noTimeoutWall, longMax) + grace;

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

    private static TimeSpan Duration(string text) => RecipeDuration.TryFrom(text, out var d) ? d.Span : throw new ArgumentException($"not a duration: {text}", nameof(text));

    private static int Percent(string text) => int.Parse(text.AsSpan(0, text.Length - 1), NumberStyles.None, CultureInfo.InvariantCulture);

    private static long MemoryMax(string text, long ram) =>
        string.Equals(text, "auto", StringComparison.Ordinal) ? Math.Min(ram / 5, 12 * Gib) : Bytes(text);

    private static long Total(string text, long ram) => text.EndsWith('%') ? ram / 100 * Percent(text) : Bytes(text);

    private static long Bytes(string size)
    {
        var m = SizeShape.Match(size);
        if (!m.Success)
        {
            throw new ArgumentException($"not a size: {size}", nameof(size));
        }

        var n = long.Parse(m.Groups["n"].Value, NumberStyles.None, CultureInfo.InvariantCulture);
        return m.Groups["u"].Value switch
        {
            "KiB" => n * 1024,
            "MiB" => n * 1024 * 1024,
            "GiB" => n * Gib,
            _ => n * Gib * 1024,
        };
    }

    [GeneratedRegex("^(?<n>[1-9][0-9]*)(?<u>KiB|MiB|GiB|TiB)\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex SizeShape { get; }
}
