using System.Globalization;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Trust;
using Citation = Aw.Context.Core.Results.Citation;

namespace Aw.Context.Features.GetLenses;

/// <summary>
/// The intent band's records, cited (#1702): each one the record store still holds live gets a marker 'C1', 'C2', ... in band order and a citation with its permalink and
/// aliases, read the way a query's citations are (<see cref="RecordPermalinks"/>), so the app can offer "Open in Linear". A record the store doesn't hold, holds removed or blocked,
/// or can't be read stays uncited ('Cites' empty, state unchecked): the answer never fails on it.
/// </summary>
internal static class LensCitations
{
    /// <summary>What the reads need.</summary>
    /// <param name="Store">The record store.</param>
    /// <param name="Teams">The profile's Linear team keys.</param>
    /// <param name="Egress">The per-field scrubber.</param>
    /// <param name="AsOf">When the answer is built.</param>
    internal sealed record Reading(IRecordStore Store, IReadOnlySet<string> Teams, IEgressScrubber Egress, DateTimeOffset AsOf);

    private const string Prefix = "record:";

    // The source id the query path gives a stored record (its slice is not reachable from here).
    private static readonly SourceId _recordStore = SourceId.From("records");

    /// <summary>Cites the band's records.</summary>
    /// <param name="intent">The intent band.</param>
    /// <param name="reading">What the reads need.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The band with its records' markers and the citations by marker.</returns>
    internal static async ValueTask<(IntentBand Band, IReadOnlyDictionary<string, Citation> Cites)> Cite(IntentBand intent, Reading reading, CancellationToken cancellationToken)
    {
        var cites = new Dictionary<string, Citation>(StringComparer.Ordinal);
        var records = new List<LensRecord>(intent.Records.Count);
        foreach (var record in intent.Records)
        {
            var citation = await Read(record, reading, cancellationToken).ConfigureAwait(false);
            if (citation is null)
            {
                records.Add(record);
                continue;
            }

            var marker = "C" + (cites.Count + 1).ToString(CultureInfo.InvariantCulture);
            cites[marker] = citation;
            records.Add(record with { Cites = [marker], State = CitationState.Current });
        }

        return (intent with { Records = records }, cites);
    }

    private static async ValueTask<Citation?> Read(LensRecord record, Reading reading, CancellationToken cancellationToken)
    {
        var name = record.Key.Value;
        if (!name.StartsWith(Prefix, StringComparison.Ordinal) || !RecordKey.TryFrom(name[Prefix.Length..], out var key))
        {
            return null;
        }

        var read = await reading.Store.Get(key, cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { RemovedAt: null, Blocked: false } stored || !ExternalId.TryFrom(stored.Key.Value, out var id))
        {
            return null;
        }

        var link = RecordPermalinks.Of(stored, reading.Teams, reading.Egress);
        var reference = new CitationRef(_recordStore, ExternalId.From(Scrubbed(reading.Egress, id.Value)), SourceVersion.From(Scrubbed(reading.Egress, stored.Version.Value))) { FetchedAt = reading.AsOf };
        return new Citation(RecordKey.From(Scrubbed(reading.Egress, stored.Key.Value)), [reference], link.Permalink, Core.Results.Trust.Untrusted, CitationState.Current) { Aliases = link.Aliases };
    }

    private static string Scrubbed(IEgressScrubber egress, string value) => egress.Scrub(value) is { Blocked: false } scrubbed ? scrubbed.Text : "scrub:blocked";
}
