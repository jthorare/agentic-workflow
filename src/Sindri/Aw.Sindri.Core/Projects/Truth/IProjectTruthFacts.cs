using Aw.Kernel;

namespace Aw.Sindri.Core.Projects.Truth;

/// <summary>Sindri's own PR, CI, item and agent data for the Projects page. One read per page, never one per project.</summary>
public interface IProjectTruthFacts
{
    /// <summary>Reads the facts for the union of every card's sources.</summary>
    /// <param name="union">The sources of all cards together.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The facts, or the error.</returns>
    public ValueTask<Result<ProjectTruthFacts>> Read(ProjectSources union, CancellationToken cancellationToken);
}
