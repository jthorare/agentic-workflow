
namespace Aw.Sindri.Core.Launch;

/// <summary>
/// Which workflow instance, Step and attempt a launch belongs to, so a client can show "on &lt;instance&gt; · &lt;state&gt;" with no extra call. Served as 'step' on a launch
/// (its own 'state' is taken by requested/started/failed) and as 'launch' on a session row. Every member is null for an attended (click) launch.
/// </summary>
/// <param name="Instance">The workflow instance address, or null.</param>
/// <param name="State">The Step's state id in that instance, or null.</param>
/// <param name="Attempt">The Step run's number ('{entry}.{n}' gives n), or null.</param>
public sealed record LaunchStep(InstanceAddress? Instance = null, string? State = null, int? Attempt = null)
{
    /// <summary>A launch that belongs to no instance: attended, or recorded before the fields existed.</summary>
    public static LaunchStep None { get; } = new();
}
