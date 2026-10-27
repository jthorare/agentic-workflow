namespace Aw.Context.Core.Glossary;

/// <summary>Single-link agglomerative clustering over unit centroids.</summary>
internal static class GlossaryClustering
{
    // Groups of indexes into the centroid list, each in index order, ordered by their first index. The list is in key order, which keeps this stable.
    internal static List<List<int>> Group(IReadOnlyList<Centroid> centroids, double similarity)
    {
        var parent = Enumerable.Range(0, centroids.Count).ToArray();
        for (var i = 0; i < centroids.Count; i++)
        {
            for (var j = i + 1; j < centroids.Count; j++)
            {
                if (GlossaryCentroids.Dot(centroids[i].Unit, centroids[j].Unit) >= similarity)
                {
                    Union(parent, i, j);
                }
            }
        }

        var groups = new Dictionary<int, List<int>>();
        for (var i = 0; i < centroids.Count; i++)
        {
            var root = Root(parent, i);
            if (!groups.TryGetValue(root, out var members))
            {
                groups[root] = members = [];
            }

            members.Add(i);
        }

        return [.. groups.OrderBy(g => g.Value[0]).Select(g => g.Value)];
    }

    private static void Union(int[] parent, int a, int b)
    {
        var (low, high) = (Root(parent, a), Root(parent, b));
        if (low > high)
        {
            (low, high) = (high, low);
        }

        parent[high] = low;
    }

    // The root is always the lowest index in its group, so the smaller root absorbs the larger.
    private static int Root(int[] parent, int i)
    {
        while (parent[i] != i)
        {
            parent[i] = parent[parent[i]];
            i = parent[i];
        }

        return i;
    }
}
