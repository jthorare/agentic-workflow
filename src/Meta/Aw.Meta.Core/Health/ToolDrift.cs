namespace Aw.Meta.Core.Health;

/// <summary>One installed tool compared with what origin/main would install (issue #4249).</summary>
/// <param name="Tool">The tool: heavy, meta-gate-reserve, aw-meta, judge or sindri.</param>
/// <param name="State">How it compares.</param>
/// <param name="Detail">What was compared, and the fix when there is one, in one line.</param>
public sealed record ToolDrift(string Tool, DriftState State, string Detail)
{
    /// <summary>The line health prints for it: "tool: state: detail".</summary>
    /// <returns>The line.</returns>
    public string Line() => Tool + ": " + Word(State) + ": " + Detail;

    private static string Word(DriftState state) => state switch
    {
        DriftState.Current => "current",
        DriftState.Stale => "stale",
        DriftState.Unknown => "unknown",
        _ => "unknown",
    };
}
