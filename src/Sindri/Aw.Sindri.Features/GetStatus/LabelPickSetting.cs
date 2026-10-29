using System.Text.Json;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.GetStatus;

/// <summary>'context.models.labeling.pick' of the approved profile, as the Settings page says it (model routing spec §8.5): 'off', 'on', or 'needs-key' when it is on and Jev has no key. Computed here so the app never derives it.</summary>
internal static class LabelPickSetting
{
    internal const string Off = "off";
    internal const string On = "on";
    internal const string NeedsKey = "needs-key";

    /// <summary>The verdict for the approved profile.</summary>
    /// <param name="approved">The approved profile, or null when none is approved (the pick is off).</param>
    /// <param name="keys">Says whether Jev has a key; null counts it present.</param>
    /// <returns>'off', 'on' or 'needs-key'.</returns>
    internal static string Of(LoadedProfile? approved, IJevKeyProbe? keys) =>
        !IsJev(approved?.Profile.Context) ? Off : keys is { } probe && !probe.Present() ? NeedsKey : On;

    // The Context section is checked by the context engine at validate and approve; here only the one key is read, so a section that doesn't say 'jev' is off.
    private static bool IsJev(JsonElement? context) =>
        context is { ValueKind: JsonValueKind.Object } section
        && section.TryGetProperty("models", out var models) && models.ValueKind == JsonValueKind.Object
        && models.TryGetProperty("labeling", out var labeling) && labeling.ValueKind == JsonValueKind.Object
        && labeling.TryGetProperty("pick", out var pick) && pick.ValueKind == JsonValueKind.String
        && string.Equals(pick.GetString(), "jev", StringComparison.Ordinal);
}
