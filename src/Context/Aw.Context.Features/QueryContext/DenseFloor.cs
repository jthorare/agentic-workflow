using Aw.Context.Core.Vectors;

namespace Aw.Context.Features.QueryContext;

/// <summary>
/// The dense leg's relevance floor (spec §6.1): a nearest-neighbour scan always returns its rows, relevant or not, so what is far is cut. Everything within
/// cosine distance 0.5 is kept, else the nearest 8 within 0.75, else nothing. The numbers are ConnectAI's starting values; the spec makes them profile keys
/// set by the I4b backtest, so they are constants until that lands.
/// </summary>
internal static class DenseFloor
{
    /// <summary>Hits at or under this cosine distance are all kept.</summary>
    public const float Keep = 0.5f;

    /// <summary>When none is within <see cref="Keep"/>, the nearest <see cref="RescueCount"/> at or under this distance are kept.</summary>
    public const float Rescue = 0.75f;

    /// <summary>How many hits the rescue keeps.</summary>
    public const int RescueCount = 8;

    /// <summary>Applies the floor.</summary>
    /// <param name="hits">Scan hits, best first.</param>
    /// <returns>The hits that stay, in the same order.</returns>
    public static IReadOnlyList<VectorHit> Apply(IReadOnlyList<VectorHit> hits)
    {
        ArgumentNullException.ThrowIfNull(hits);
        var near = hits.Where(h => Distance(h) <= Keep).ToList();
        return near.Count > 0 ? near : [.. hits.Where(h => Distance(h) <= Rescue).Take(RescueCount)];
    }

    private static float Distance(VectorHit hit) => 1f - hit.Score;
}
