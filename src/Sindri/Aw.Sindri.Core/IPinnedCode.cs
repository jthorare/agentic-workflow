using Aw.Kernel;

namespace Aw.Sindri.Core;

/// <summary>The code files one concept pin holds in the current map, for a code-index search inside the pin (concept pins spec §3.5). Plain types: Core never names the context engine's.</summary>
public interface IPinnedCode
{
    /// <summary>The (repo, repo-relative path) pairs the map's own grouping puts in <paramref name="concept"/>.</summary>
    /// <param name="concept">A pin id, or 'concept:&lt;id&gt;'.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The files; SND-CONTEXT-102 to 104 from the pin lookup, 105 when there is no map, else the map read's errors.</returns>
    public ValueTask<Result<PinnedCode>> Files(string concept, CancellationToken cancellationToken);
}
