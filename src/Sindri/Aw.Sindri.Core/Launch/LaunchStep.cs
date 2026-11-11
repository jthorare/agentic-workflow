
namespace Aw.Sindri.Core.Launch;

/// <summary>
/// Which workflow instance, Step and attempt a launch belongs to, so a client can show "on &lt;instance&gt; · &lt;state&gt;" with no extra call. Served as 'step' on a launch
/// (its own 'state' is taken by requested/started/failed) and as 'launch' on a session row. Every member is null for an attended (click) launch.
/// </summary>
/// <param name="Instance">The workflow instance address, or null.</param>
/// <param name="State">The Step's state id in that instance, or null.</param>
/// <param name="Attempt">The Step run's number ('{entry}.{n}' gives n), or null.</param>
/// <param name="Run">The graph run's id when the launch is a 'run_agent' node run's (PR 7), or null; left out of the JSON when null.</param>
/// <param name="Node">The node's id in that run, or null; left out of the JSON when null. A node launch has no <paramref name="Instance"/>, and its <paramref name="State"/> is the node's id too.</param>
public sealed record LaunchStep(
    InstanceAddress? Instance = null,
    string? State = null,
    int? Attempt = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? Run = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? Node = null)
{
    /// <summary>A launch that belongs to no instance: attended, or recorded before the fields existed.</summary>
    public static LaunchStep None { get; } = new();
}
