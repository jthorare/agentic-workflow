using System.Globalization;
using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Views;

/// <summary>The projector's status: GET /status 'views', the doctor line and the health (API spec §2.2, §2.3).</summary>
/// <param name="Cursor">The last ledger sequence the views include.</param>
/// <param name="LedgerHead">The ledger's head as the projector last saw it.</param>
/// <param name="ProjectedAt">When the cursor last moved, or null before the first event.</param>
/// <param name="Rebuilding">True while the views are rebuilding.</param>
/// <param name="Stalled">True when the projector stopped on an event, or (with a null <paramref name="StalledAt"/>) couldn't run at all.</param>
/// <param name="StalledAt">The sequence it stopped at, when stalled on an event.</param>
/// <param name="StalledType">That event's type, when stalled on an event; else why it can't run, as 'code: detail' with the failing call's own code (SND-VIEW-001 for views.db, SND-LEDGER-00x for the ledger).</param>
/// <param name="Starting">True from boot until the projector's first publish: not Healthy yet, but not a rebuild either, so commands wait for the first pass. Never serialized (#2053).</param>
public sealed record ViewsStatus(long Cursor, long LedgerHead, DateTimeOffset? ProjectedAt, bool Rebuilding, bool Stalled,
    long? StalledAt, string? StalledType, [property: JsonIgnore] bool Starting = false)
{
    /// <summary>The freshness a view answer carries.</summary>
    [JsonIgnore]
    public AsOf AsOf => new(Cursor, LedgerHead, ProjectedAt, Rebuilding, Stalled);

    /// <summary>What stopped the views, after the word 'views': 'stalled at N (type)' for an event, else 'can't be updated (why)'.</summary>
    /// <returns>The phrase.</returns>
    public string StalledPhrase() => StalledAt is { } at
        ? string.Create(CultureInfo.InvariantCulture, $"stalled at {at} ({StalledType})")
        : $"can't be updated ({StalledType})";

    /// <summary>The command that fixes a stall: the doctor when the ledger is what can't be read, else a rebuild.</summary>
    /// <returns>The command.</returns>
    public string StalledFix() => EmptyText.FixFor(StalledType);

    /// <summary>The sentence for the stalled or rebuilding views (API spec §3.6), or null when they are fresh.</summary>
    public string? Text => Stalled ? (StalledAt is null ? EmptyText.Failing(StalledType) : EmptyText.Stalled(StalledAt.Value)) : (Rebuilding ? EmptyText.Rebuilding(Cursor, LedgerHead) : null);
}
