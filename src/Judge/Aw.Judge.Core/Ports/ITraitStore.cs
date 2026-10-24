using Aw.Judge.Core.Traits;

namespace Aw.Judge.Core.Ports;

/// <summary>Where a repo's traits live: '&lt;repo&gt;/.agents/traits/*.yaml', overlaid by '&lt;state&gt;/traits/&lt;repo-name&gt;/*.yaml'.</summary>
public interface ITraitStore
{
    /// <summary>
    /// Reads both directories, the overlay last so that its trait replaces a repo trait of the same name. A missing
    /// directory has no traits and a file that isn't a valid trait is a problem, never an exception.
    /// </summary>
    /// <param name="repoRoot">The repo's top-level directory.</param>
    /// <param name="repoName">The repo's name (the overlay directory's name).</param>
    /// <returns>The traits and the files that were skipped.</returns>
    public TraitSet Load(string repoRoot, string repoName);
}
