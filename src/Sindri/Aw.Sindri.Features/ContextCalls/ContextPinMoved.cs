using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>What the newest refresh moved in and out of one pin: the features it gained and lost, where the lost ones went, and what the relink after it did to the pin's records.</summary>
/// <param name="FromVersion">The version the refresh replaced ('v41').</param>
/// <param name="ToVersion">The version it published, always the current one ('v42').</param>
/// <param name="Gained">Features that joined the pin.</param>
/// <param name="Lost">Features that left it.</param>
/// <param name="To">Where the lost features went, the biggest first, at most two; a null id and name is 'in no concept'.</param>
/// <param name="RecordsLinked">Records linked to the pin after the relink minus before it (signed); null while no relink of <paramref name="ToVersion"/> has succeeded.</param>
internal sealed record ContextPinMoved(string FromVersion, string ToVersion, int Gained, int Lost, IReadOnlyList<ContextPinMovedTo> To, int? RecordsLinked)
{
    /// <summary>True when the newest ended relink after the refresh failed, so <see cref="RecordsLinked"/> stays null until one succeeds.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool RelinkFailed { get; init; }
}
