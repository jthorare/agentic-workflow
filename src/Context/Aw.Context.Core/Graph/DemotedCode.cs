using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Graph;

/// <summary>Whether a code node is a file its repo demotes (<see cref="ContextRepo.Demotes"/>): it keeps its node and a direct read, but is never an anchor or a suggestion.</summary>
public static class DemotedCode
{
    private const string CodePrefix = "code:";

    /// <summary>Whether <paramref name="key"/> is a code node of a file one of <paramref name="repos"/> demotes.</summary>
    /// <param name="key">A node key.</param>
    /// <param name="repos">The repos in scope.</param>
    /// <returns>True for a demoted file's code node.</returns>
    public static bool IsDemoted(NodeKey key, IReadOnlyList<ContextRepo> repos)
    {
        ArgumentNullException.ThrowIfNull(repos);
        return key.Kind == NodeKind.Code && repos.Any(r => r.DemotePaths.Count > 0 && r.Demotes(key.Value[CodePrefix.Length..]));
    }
}
