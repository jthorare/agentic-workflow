using System.Text.Json;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.GetStatus;

/// <summary>'context.models.labeling.pick' (or 'context.models.productSurfaces.pick') of the approved profile, as the Settings page says it (model routing spec §8.5): 'off', 'on', or 'needs-key' when it is on and Jev has no key. Computed here so the app never derives it.</summary>
internal static class LabelPickSetting
{
    internal const string Off = "off";
    internal const string On = "on";
    internal const string NeedsKey = "needs-key";
    internal const string Labeling = "labeling";
    internal const string ProductSurfaces = "productSurfaces";

    /// <summary>The verdict for the approved profile.</summary>
    /// <param name="approved">The approved profile, or null when none is approved (the pick is off).</param>
    /// <param name="keys">Says whether Jev has a key; null counts it present.</param>
    /// <param name="cancellationToken">Cancels the key check.</param>
    /// <param name="kind">The 'context.models' key whose pick is read: 'labeling' or 'productSurfaces'.</param>
    /// <returns>'off', 'on' or 'needs-key'.</returns>
    internal static async ValueTask<string> Of(LoadedProfile? approved, IJevKeyProbe? keys, CancellationToken cancellationToken, string kind = Labeling) =>
        !IsJev(approved?.Profile.Context, kind) ? Off : keys is { } probe && !await probe.Present(cancellationToken).ConfigureAwait(false) ? NeedsKey : On;

    // The Context section is checked by the context engine at validate and approve; here only the one key is read, so a section that doesn't say 'jev' is off.
    private static bool IsJev(JsonElement? context, string kind) =>
        context is { ValueKind: JsonValueKind.Object } section
        && section.TryGetProperty("models", out var models) && models.ValueKind == JsonValueKind.Object
        && models.TryGetProperty(kind, out var entry) && entry.ValueKind == JsonValueKind.Object
        && entry.TryGetProperty("pick", out var pick) && pick.ValueKind == JsonValueKind.String
        && string.Equals(pick.GetString(), "jev", StringComparison.Ordinal);
}
