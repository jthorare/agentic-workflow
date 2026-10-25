using System.Buffers;
using System.Globalization;
using System.Text.Json;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Concepts;

/// <summary>Builds the concept nodes and their belongs-to edges from the plan, the names and what each record mentions.</summary>
internal static class ConceptNodes
{
    private const int NameLength = 60;
    private const int SummaryFeatures = 3;
    private const int EvidenceCap = 25;

    internal sealed record Built(GraphNode Node, PlannedConcept Plan);

    internal static Built Concept(PlannedConcept plan, ConceptView view, ConceptNames? names)
    {
        var key = NodeKey.From("concept:" + plan.Slug);
        if (plan.Pin is { } pin)
        {
            var label = Clean(pin.Name) is { Length: > 0 } pinned ? pinned : Humanize(plan.Slug);
            return new Built(new GraphNode(key, label, Provenance.Human, 1.0, Evidence(plan, view), Attributes(pin.Summary ?? Summary(plan, view), "override", plan.Seed)), plan);
        }

        if (names?.BySeed.GetValueOrDefault(plan.Seed) is { } named && Clean(named.Name) is { Length: > 0 } good && !LooksLikeIdentifier(good))
        {
            var summary = string.IsNullOrWhiteSpace(named.Summary) ? Summary(plan, view) : named.Summary.Trim();
            return new Built(new GraphNode(key, good, Provenance.Model, 0.7, Evidence(plan, view), Attributes(summary, "derived", plan.Seed)), plan);
        }

        return new Built(new GraphNode(key, Humanize(view.Label(plan.Seed)), Provenance.Deterministic, 0.5, Evidence(plan, view), Attributes(Summary(plan, view), "derived", plan.Seed)), plan);
    }

    // Every feature goes to the concept (or concepts) that hold it; a record or decision goes with the features it mentions or decides, else to 'concept:other'.
    internal static ExtractedMap Join(ConceptView view, List<Built> concepts)
    {
        var byFeature = new Dictionary<string, List<Built>>(StringComparer.Ordinal);
        foreach (var built in concepts)
        {
            foreach (var feature in built.Plan.Features)
            {
                _ = byFeature.TryAdd(feature, []);
                byFeature[feature].Add(built);
            }
        }

        var other = Other();
        var hasOther = view.FeatureKeys.Count > 0;
        var edges = concepts.SelectMany(c => c.Plan.Features.Select(f => Belongs(NodeKey.From(f), c.Node))).ToList();
        foreach (var intent in view.Intent)
        {
            var homes = view.FeaturesOf(intent.Key.Value).SelectMany(f => byFeature.GetValueOrDefault(f) ?? []).DistinctBy(c => c.Node.Key.Value, StringComparer.Ordinal).ToList();
            if (homes.Count > 0)
            {
                edges.AddRange(homes.Select(c => Belongs(intent.Key, c.Node)));
            }
            else if (hasOther)
            {
                edges.Add(Belongs(intent.Key, other));
            }
        }

        var nodes = concepts.Select(c => WithIntentEvidence(c, edges, view)).ToList();
        if (hasOther)
        {
            nodes.Add(other);
        }

        return new ExtractedMap(nodes, edges).Plus(ExtractedMap.Empty);
    }

    // The records that name a concept are cited on it beside its features' evidence.
    private static GraphNode WithIntentEvidence(Built built, List<GraphEdge> edges, ConceptView view)
    {
        var records = edges.Where(e => e.To.Value == built.Node.Key.Value && e.From.Kind == NodeKind.Record).Select(e => view.Node(e.From.Value)).OfType<GraphNode>().Select(ConceptView.Cites).OfType<EvidenceRef>();
        var all = built.Node.EvidenceOrEmpty.Concat(records).DistinctBy(e => (e.Key.Value, e.Span)).Take(EvidenceCap).ToList();
        return built.Node with { Evidence = all.Count == 0 ? null : all };
    }

    private static GraphNode Other() =>
        new(NodeKey.From(ConceptKeys.Other), "Other", Provenance.Deterministic, 0.5, Evidence: null, Attributes("Everything no other concept takes.", "derived", ""));

    private static GraphEdge Belongs(NodeKey member, GraphNode concept) => new(member, concept.Key, EdgeType.BelongsTo, concept.Provenance, concept.Confidence);

    private static List<EvidenceRef>? Evidence(PlannedConcept plan, ConceptView view)
    {
        var refs = plan.Features.SelectMany(f => view.Node(f)?.EvidenceOrEmpty ?? []).DistinctBy(e => (e.Key.Value, e.Span)).Take(EvidenceCap).ToList();
        return refs.Count == 0 ? null : refs;
    }

    private static string Summary(PlannedConcept plan, ConceptView view)
    {
        var shown = string.Join(", ", plan.Features.Take(SummaryFeatures).Select(view.Label));
        var more = plan.Features.Count > SummaryFeatures ? " …" : "";
        var count = plan.Features.Count.ToString(CultureInfo.InvariantCulture);
        return plan.Features.Count == 1 ? $"1 feature: {shown}" : $"{count} features: {shown}{more}";
    }

    private static JsonElement Attributes(string summary, string source, string seed)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("summary", summary);
            writer.WriteString("source", source);
            writer.WriteString("seed", seed);
            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }

    // A path segment or an identifier is not a capability a person names: camelCase, or a '.', '/' or '_' in it.
    private static bool LooksLikeIdentifier(string name)
    {
        if (name.AsSpan().IndexOfAny("./_") >= 0)
        {
            return true;
        }

        for (var i = 1; i < name.Length; i++)
        {
            if (char.IsLower(name[i - 1]) && char.IsUpper(name[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static string Clean(string text)
    {
        var plain = new string([.. text.Select(c => char.IsControl(c) ? ' ' : c)]);
        var joined = string.Join(' ', plain.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return joined.Length > NameLength ? joined[..NameLength].TrimEnd() : joined;
    }

    private static string Humanize(string text)
    {
        var words = string.Join(' ', text.Split(['-', '_', '.', ' '], StringSplitOptions.RemoveEmptyEntries));
        return words.Length == 0 ? text : string.Concat(words[..1].ToUpper(CultureInfo.InvariantCulture), words.AsSpan(1));
    }
}
