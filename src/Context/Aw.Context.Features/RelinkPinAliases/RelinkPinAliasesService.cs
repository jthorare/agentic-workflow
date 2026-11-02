using System.Globalization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Map;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.RelinkPinAliases;

/// <summary>
/// The pin-alias relink (concept pins spec §3.4): for every stored record that is in the intent layer (not removed, not blocked), runs <see cref="RulesLinker.Aliases"/> over its stored chunks against the
/// current map and the approved pins, and where the result differs from the record's alias links writes it with <see cref="IIntentStore.ReplaceRuleLinks"/>. It runs no other rule, asks no model, embeds and fetches
/// nothing, and leaves every other link, the extraction state and each record's map version alone.
/// </summary>
internal sealed class RelinkPinAliasesService(MapReader graphs, IIntentStore intents, IRecordStore records, TimeProvider clock) : IService<RelinkPinAliasesRequest, RelinkPinAliasesResult>
{
    private const int PageSize = 100;

    public async ValueTask<Result<RelinkPinAliasesResult>> Handle(RelinkPinAliasesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var started = clock.GetTimestamp();
        var pins = PinAliasSet.Of(request.Scope);
        if (pins.IsError)
        {
            return pins.Error;
        }

        var map = await graphs.ReadWithoutIntent(request.Scope, draft: false, cancellationToken).ConfigureAwait(false);
        var layer = await intents.Layer(cancellationToken).ConfigureAwait(false);
        if (map.IsError || layer.IsError)
        {
            return map.IsError ? map.Error : layer.Error;
        }

        var walked = await Walk((map.Value.Graph, pins.Value), layer.Value, cancellationToken).ConfigureAwait(false);
        if (walked.IsError)
        {
            return walked.Error;
        }

        var (scanned, changed) = walked.Value;
        return new RelinkPinAliasesResult(PinAliasSet.Hash(pins.Value), map.Value.Snapshot.Version.Number, scanned.Count, changed, (long)clock.GetElapsedTime(started).TotalMilliseconds, Counts(pins.Value, layer.Value, scanned));
    }

    // Every stored record that is live and in the intent layer, a page at a time.
    private async ValueTask<Result<(List<(RecordKey Key, IReadOnlyList<IntentLink> Links)> Scanned, int Changed)>> Walk((GraphSnapshot Graph, IReadOnlyList<PinAliases> Pins) world, IntentLayer layer, CancellationToken cancellationToken)
    {
        var inLayer = layer.Records.Select(static r => r.Key.Value).ToHashSet(StringComparer.Ordinal);
        var scanned = new List<(RecordKey Key, IReadOnlyList<IntentLink> Links)>();
        var changed = 0;
        RecordKey? after = null;
        while (true)
        {
            var page = await records.List(new RecordFilter(), after, PageSize, cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return page.Error;
            }

            foreach (var stored in page.Value.Where(r => r.RemovedAt is null && !r.Blocked && inLayer.Contains(r.Key.Value)))
            {
                var one = await Relink(stored.Key, world, layer, cancellationToken).ConfigureAwait(false);
                if (one.IsError)
                {
                    return one.Error;
                }

                scanned.Add((stored.Key, one.Value.Links));
                changed += one.Value.Changed ? 1 : 0;
            }

            if (page.Value.Count < PageSize)
            {
                return (scanned, changed);
            }

            after = page.Value[^1].Key;
        }
    }

    private async ValueTask<Result<(IReadOnlyList<IntentLink> Links, bool Changed)>> Relink(RecordKey key, (GraphSnapshot Graph, IReadOnlyList<PinAliases> Pins) world, IntentLayer layer, CancellationToken cancellationToken)
    {
        var chunks = await records.Chunks(key, cancellationToken).ConfigureAwait(false);
        if (chunks.IsError)
        {
            return chunks.Error;
        }

        var text = string.Join('\n', chunks.Value.OrderBy(static c => c.Seq).Select(static c => c.Text));   // scrubbed at ingest
        var found = RulesLinker.Aliases(key, text, world.Graph, world.Pins);
        var stored = layer.Links.Where(l => l.Record == key && l.Rule == LinkRule.Alias).ToList();
        if (Same(stored, found))
        {
            return (found, false);
        }

        var written = await intents.ReplaceRuleLinks(key, LinkRule.Alias, found, cancellationToken).ConfigureAwait(false);
        return written.IsError ? written.Error : (found, true);
    }

    private static bool Same(List<IntentLink> stored, IReadOnlyList<IntentLink> found) =>
        stored.Count == found.Count && stored.Select(Identity).Order(StringComparer.Ordinal).SequenceEqual(found.Select(Identity).Order(StringComparer.Ordinal), StringComparer.Ordinal);

    private static string Identity(IntentLink link) => string.Create(CultureInfo.InvariantCulture, $"{link.Target.Value}|{link.Confidence:R}|{link.SpanHash}");

    // Before: the distinct records linked to concept:<id> by any rule in the layer. After: the same with each scanned record's alias links swapped for the new ones.
    private static List<PinLinkCount> Counts(IReadOnlyList<PinAliases> pins, IntentLayer layer, List<(RecordKey Key, IReadOnlyList<IntentLink> Links)> scanned)
    {
        var rescanned = scanned.Select(static s => s.Key.Value).ToHashSet(StringComparer.Ordinal);
        var counts = new List<PinLinkCount>(pins.Count);
        foreach (var pin in pins)
        {
            var target = "concept:" + pin.Id.Value;
            var before = layer.Links.Where(l => l.Target.Value == target).Select(static l => l.Record.Value).ToHashSet(StringComparer.Ordinal);
            var after = layer.Links.Where(l => l.Target.Value == target && (l.Rule != LinkRule.Alias || !rescanned.Contains(l.Record.Value))).Select(static l => l.Record.Value).ToHashSet(StringComparer.Ordinal);
            after.UnionWith(scanned.SelectMany(static s => s.Links).Where(l => l.Target.Value == target).Select(static l => l.Record.Value));
            counts.Add(new PinLinkCount(pin.Id, before.Count, after.Count));
        }

        return counts;
    }
}
