using System.Globalization;
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

    /// <summary>The group's stable key: the sha256 (lowercase hex) of its candidates' node keys and evidence, each sorted. The same candidates with the same evidence give the same key, so the context lane can skip a group it already decided.</summary>
    public string Key => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical())));

    private string Canonical()
    {
        var text = new StringBuilder();
        foreach (var candidate in Candidates.OrderBy(static c => c.NodeKey, StringComparer.Ordinal).ThenBy(static c => c.Name, StringComparer.Ordinal))
        {
            _ = text.Append("node ").Append(candidate.NodeKey.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(candidate.NodeKey).Append('\n');
            foreach (var path in candidate.Evidence.Order(StringComparer.Ordinal))
            {
                _ = text.Append("evidence ").Append(path.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(path).Append('\n');
            }
        }

        return text.ToString();
    }
}
