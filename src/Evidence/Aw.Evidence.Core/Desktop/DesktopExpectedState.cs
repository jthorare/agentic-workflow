using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Desktop;

/// <summary>The state a step's target must reach, as the driver's wait-for verb knows it.</summary>
/// <param name="State">One of <see cref="States"/>.</param>
/// <param name="Text">The text for textEquals and textContains.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DesktopExpectedState(string State, string? Text = null)
{
    /// <summary>The states the driver waits for.</summary>
    public static IReadOnlyList<string> States { get; } = ["exists", "visible", "hidden", "gone", "enabled", "disabled", "textEquals", "textContains"];

    /// <summary>The states that compare text.</summary>
    /// <param name="state">A state.</param>
    /// <returns>Whether it needs text.</returns>
    public static bool NeedsText(string state) => string.Equals(state, "textEquals", StringComparison.Ordinal) || string.Equals(state, "textContains", StringComparison.Ordinal);
}
