using System.Security.Cryptography;
using System.Text;
using Aw.Context.Core.Vectors;

namespace Aw.Context.Core.ConceptSynthesis;

/// <summary>Groups records whose vectors are near each other across sources.</summary>
public static class ConceptGrouper
{
    /// <summary>Joins each record to its near neighbours and keeps the connected groups.</summary>
    /// <param name="records">The record vectors, all of one dimension.</param>
    /// <param name="settings">The knobs.</param>
    /// <param name="scan">The vector scan.</param>
    /// <returns>The groups and every neighbour cosine seen.</returns>
    public static GroupingResult Group(IReadOnlyList<RecordVector> records, GroupingSettings settings, IVectorScan scan)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(scan);
        if (records.Count == 0)
        {
            return new GroupingResult([], []);
        }

        var edges = NearestEdges(records, settings, scan);
        var parts = new List<List<int>>();
        Split([.. Enumerable.Range(0, records.Count)], edges, settings.MinCosine, settings, parts);

        var groups = parts
            .Select(p => Build(p.Select(i => records[i])))
            .Where(g => g.Members.Count >= settings.MinRecords && g.SourceCount >= settings.MinSources)
            .OrderBy(g => g.Members[0].Record, StringComparer.Ordinal)
            .ToList();
        return new GroupingResult(groups, [.. edges.Select(e => e.Cosine)]);
    }

    // Each record's nearest neighbours, every pair once (the scan's score is the cosine: the vectors are unit length).
    private static List<Edge> NearestEdges(IReadOnlyList<RecordVector> records, GroupingSettings settings, IVectorScan scan)
    {
        var dim = records[0].Vector.Length;
        var data = new float[records.Count * dim];
        for (var i = 0; i < records.Count; i++)
        {
            if (records[i].Vector.Length != dim)
            {
                throw new ArgumentException("every record vector must have the same dimension", nameof(records));
            }

            records[i].Vector.CopyTo(data.AsMemory(i * dim, dim));
        }

        var matrix = new VectorMatrix(dim, data, [.. records.Select(r => r.Record)]);
        var count = Math.Clamp(settings.Neighbours + 1, 1, Math.Min(records.Count, 1000));
        var seen = new HashSet<(int, int)>();
        var edges = new List<Edge>();
        for (var i = 0; i < records.Count; i++)
        {
            var top = scan.Top(matrix, records[i].Vector, ReadOnlyMemory<bool>.Empty, count);
            if (top.IsError)
            {
                throw new InvalidOperationException($"the vector scan failed: {top.Error.Code}");
            }

            foreach (var hit in top.Value.Where(h => h.Row != i))
            {
                var pair = (Math.Min(i, hit.Row), Math.Max(i, hit.Row));
                if (seen.Add(pair))
                {
                    edges.Add(new Edge(pair.Item1, pair.Item2, hit.Score));
                }
            }
        }

        return edges;
    }

    // Connected components at the threshold; one over the cap is split again on its own edges at a tighter threshold, until every part fits.
    private static void Split(List<int> nodes, List<Edge> edges, float threshold, GroupingSettings settings, List<List<int>> parts)
    {
        var parent = nodes.ToDictionary(n => n, n => n);
        int Find(int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }

            return x;
        }

        foreach (var edge in edges.Where(e => e.Cosine >= threshold))
        {
            parent[Find(edge.A)] = Find(edge.B);
        }

        foreach (var component in nodes.GroupBy(Find).OrderBy(g => g.Key))
        {
            var members = component.ToList();
            if (members.Count <= settings.MaxGroup || members.Count == 1)
            {
                parts.Add(members);
                continue;
            }

            var inside = members.ToHashSet();
            var own = edges.Where(e => e.Cosine >= threshold && inside.Contains(e.A) && inside.Contains(e.B)).ToList();
            Split(members, own, threshold + Math.Max(settings.SplitStep, 0.001f), settings, parts);
        }
    }

    private static ConceptGroup Build(IEnumerable<RecordVector> members)
    {
        var sorted = members.OrderBy(m => m.Record, StringComparer.Ordinal).ToList();
        var text = new StringBuilder();
        foreach (var member in sorted)
        {
            _ = text.Append(member.Record).Append('\t').Append(member.Version).Append('\n');
        }

        return new ConceptGroup(sorted, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()))));
    }

    private sealed record Edge(int A, int B, float Cosine);
}
