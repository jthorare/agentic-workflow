using System.Text.RegularExpressions;
using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Core.Map;

/// <summary>Where one mapped repo stood when a map version was built (table map_version_head): the commit the build read and whether the default branch held it.</summary>
/// <param name="Repo">The repo.</param>
/// <param name="Commit">The commit, 7 to 40 lowercase hex characters.</param>
/// <param name="OnDefault">Whether that commit was on the repo's default branch.</param>
public sealed partial record MapHead(ContextRepoName Repo, string Commit, bool OnDefault)
{
    /// <summary>The commit of the code index the build resolved this repo's symbol citations against; null when none held the build's commit (#1704: a stub, never set yet).</summary>
    public string? IndexCommit { get; init; }

    /// <summary>Whether <paramref name="text"/> is a commit as heads are stored: 7 to 40 lowercase hex characters.</summary>
    /// <param name="text">The text.</param>
    /// <returns>True when it is.</returns>
    public static bool IsCommit(string? text) => text is not null && Shape.IsMatch(text);

    /// <summary>Checks the commits of <paramref name="heads"/> and that no repo appears twice.</summary>
    /// <param name="heads">The heads.</param>
    /// <returns>Null when they are fit to store, else SND-CONTEXT-191 naming the repo.</returns>
    public static Error? Check(IReadOnlyList<MapHead> heads)
    {
        ArgumentNullException.ThrowIfNull(heads);
        foreach (var head in heads)
        {
            if (!IsCommit(head.Commit))
            {
                return ContextErrors.MapHeadInvalid.With($"the commit of repo '{head.Repo.Value}' is not 7 to 40 lowercase hex characters");
            }
        }

        var twice = heads.GroupBy(h => h.Repo.Value, StringComparer.Ordinal).FirstOrDefault(g => g.Skip(1).Any());
        return twice is null ? null : ContextErrors.MapHeadInvalid.With($"repo '{twice.Key}' appears twice");
    }

    [GeneratedRegex("^[0-9a-f]{7,40}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
