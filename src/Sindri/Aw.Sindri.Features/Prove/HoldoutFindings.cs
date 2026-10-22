using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Prove;

/// <summary>The findings the hold-out log and the fail log write for a hidden scenario, in terms of its visible item and never its id or text (line spec §9.3).</summary>
internal static class HoldoutFindings
{
    /// <summary>A hidden scenario that did not pass (PRV-HOLD-001).</summary>
    /// <param name="item">The visible acceptance item.</param>
    /// <param name="observed">What the app did.</param>
    /// <returns>The finding.</returns>
    internal static JsonObject Failed(string? item, string? observed) => new()
    {
        ["rule"] = "PRV-HOLD-001",
        ["item"] = item,
        ["message"] = $"{item}: a check you have not seen failed at the head: {observed}",
        ["fix"] = "fix what the observed line describes, then run the proof again",
        ["severity"] = "fail",
    };

    /// <summary>The canary token turned up outside the store (PRV-HOLD-003); the finding never repeats the token.</summary>
    /// <returns>The finding.</returns>
    internal static JsonObject Leaked() => new()
    {
        ["rule"] = "PRV-HOLD-003",
        ["item"] = null,
        ["message"] = "the canary token of the hidden set appears in the phase's commits or facts",
        ["fix"] = "the owner replaces the hidden set; nothing the builder saw can stay",
        ["severity"] = "fail",
    };
}
