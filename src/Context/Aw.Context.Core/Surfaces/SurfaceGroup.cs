using System.Security.Cryptography;
using System.Text;

namespace Aw.Context.Core.Surfaces;

/// <summary>
/// One question's worth of candidates (spec §6.9): one derived candidate, or up to <see cref="MaxCandidates"/> that look like the same thing. A merge never spans two groups. The group is cut and ordered by the pass, in key order, so a rerun builds the same groups.
/// </summary>
/// <param name="Candidates">1 to <see cref="MaxCandidates"/> candidates.</param>
public sealed record SurfaceGroup(IReadOnlyList<SurfaceCandidate> Candidates)
{
    /// <summary>The most candidates in a group: names plus 'separate' and 'reject' must fit the port's five options.</summary>
    public const int MaxCandidates = 3;

    /// <summary>
    /// The group's stable key: lowercase hex sha256 of the UTF-8 bytes built from the RAW values (before any scrub or cut). For each candidate, sorted ordinally by node key (then kind, then pass): <c lang="text">NodeKey\nKind\nPass\n</c>, then its evidence paths
    /// sorted ordinally, each followed by <c lang="text">\n</c>; the candidates' texts are concatenated. Edges and names are not in the key. The same candidates with the same evidence give the same key, so the context lane can skip a group it already decided.
    /// </summary>
    public string Key => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical())));

    private string Canonical()
    {
        var text = new StringBuilder();
        foreach (var candidate in Candidates.OrderBy(static c => c.NodeKey, StringComparer.Ordinal).ThenBy(static c => c.Kind, StringComparer.Ordinal).ThenBy(static c => c.Pass, StringComparer.Ordinal))
        {
            _ = text.Append(candidate.NodeKey).Append('\n').Append(candidate.Kind).Append('\n').Append(candidate.Pass).Append('\n');
            foreach (var path in candidate.Evidence.Order(StringComparer.Ordinal))
            {
                _ = text.Append(path).Append('\n');
            }
        }

        return text.ToString();
    }
}
