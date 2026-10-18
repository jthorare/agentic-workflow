using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>Resolves the limits of one launch from the profile and the defaults (sandbox spec §3.3, §3.9, §9).</summary>
public static class LimitsResolver
{
    private const long Gib = 1024L * 1024 * 1024;
    private static readonly TimeSpan _stepDefault = TimeSpan.FromHours(4);

    /// <summary>Resolves the limits.</summary>
    /// <param name="input">What the limits depend on.</param>
    /// <returns>The limits.</returns>
    public static PlanLimits Resolve(LimitsInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var sized = Sized(input.Limits ?? new SandboxLimits(), input.RamBytes, input.Cores);
        var (wall, runtimeMax) = input.Attended ? AttendedWall(input.AttendedSettings) : StepWall(input, sized.WallGrace, sized.ParkMax);
        return sized with { Wall = wall, RuntimeMax = runtimeMax };
    }

    private static PlanLimits Sized(SandboxLimits limits, long ram, int cores)
    {
        var memory = MemoryBytes(limits.Memory, ram);
        return new PlanLimits(
            memory,
            memory * 8 / 10,
            cores * SandboxUnits.ParsePercent(limits.Cpu ?? SandboxDefaults.Cpu),
            limits.Tasks ?? SandboxDefaults.Tasks,
            limits.Sessions ?? SandboxDefaults.Sessions,
            TotalBytes(limits.Total, ram),
            SandboxUnits.ParseSize(limits.HomeTmpfs ?? SandboxDefaults.HomeTmpfs),
            Duration(limits.WallGrace ?? SandboxDefaults.WallGrace),
            Duration(limits.ParkMax ?? SandboxDefaults.ParkMax),
            Wall: null,
            RuntimeMax: null);
    }

    private static long MemoryBytes(string? memory, long ram) =>
        memory is null || string.Equals(memory, SandboxDefaults.Memory, StringComparison.Ordinal) ? Math.Min(ram / 5, 12 * Gib) : SandboxUnits.ParseSize(memory);

    private static long TotalBytes(string? total, long ram)
    {
        var text = total ?? SandboxDefaults.Total;
        return text.EndsWith('%') ? ram * SandboxUnits.ParsePercent(text) / 100 : SandboxUnits.ParseSize(text);
    }

    private static (TimeSpan? Wall, TimeSpan? RuntimeMax) AttendedWall(SandboxAttended? settings)
    {
        var text = settings?.Wall ?? SandboxDefaults.AttendedWall;
        if (string.Equals(text, SandboxDefaults.AttendedWall, StringComparison.Ordinal))
        {
            return (null, null);
        }

        var ceiling = Duration(text);
        return (ceiling, ceiling);
    }

    private static (TimeSpan? Wall, TimeSpan? RuntimeMax) StepWall(LimitsInput input, TimeSpan grace, TimeSpan parkMax)
    {
        var wall = (input.StepTimeout ?? _stepDefault) + grace;
        if (input.LongMax is { } longMax && longMax + grace < wall)
        {
            wall = longMax + grace;
        }

        return (wall, wall + parkMax);
    }

    private static TimeSpan Duration(string text) => RecipeDuration.From(text).Span;
}
