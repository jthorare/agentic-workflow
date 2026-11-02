namespace Aw.Context.Core.Surfaces;

/// <summary>What <see cref="ISurfacePicker.Decide"/> settled (spec §6.9).</summary>
/// <param name="Verdicts">One verdict per input group, in input order.</param>
/// <param name="Counts">How the groups were settled; the numbers the call's event records.</param>
/// <param name="NotDecided">Why Jev did not decide: 'off', 'no-key' or the code the port stopped on ('token-cap', a provider code); null when Jev was asked and nothing stopped the pass.</param>
public sealed record SurfacePickResult(IReadOnlyList<SurfaceVerdict> Verdicts, SurfaceCounts Counts, string? NotDecided)
{
    /// <summary>The build's status line for a pass Jev did not decide: 'product surfaces: not decided (&lt;reason&gt;)'.</summary>
    public string? StatusLine => NotDecided is { } reason ? $"product surfaces: not decided ({reason})" : null;
}
