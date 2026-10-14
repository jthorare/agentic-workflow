namespace Aw.Sindri.Core.Events;

/// <summary>One event type in the catalog (API spec §3.4).</summary>
/// <param name="Type">The event type, for example 'process.started'.</param>
/// <param name="Public">True when the public stream and ledger reads may show it.</param>
/// <param name="PublicPayload">The payload record it carries publicly, or null when it isn't public.</param>
/// <param name="KeyShape">Its idempotency key's shape, for example 'process.started:&lt;epoch&gt;'.</param>
public sealed record EventCatalogEntry(string Type, bool Public, Type? PublicPayload, string KeyShape)
{
    /// <summary>The API views (<see cref="ApiViews"/>) the app re-queries when this event arrives (UI spec §6.7 E-15); empty for a non-public type.</summary>
    public IReadOnlyList<string> Invalidates { get; init; } = [];
}
