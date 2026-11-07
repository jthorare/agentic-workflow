using Aw.Kernel;

namespace Aw.Sindri.Core.Reviews;

/// <summary>The names of the projects on the Projects page, for the Reviews list's sections.</summary>
public interface IReviewProjects
{
    /// <summary>Reads each project's name by the key a <c language="text">review.projects[].project</c> entry uses.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Name by key (a "prj_…" id or a "linear:project/…" key), or the error.</returns>
    public ValueTask<Result<IReadOnlyDictionary<string, string>>> Names(CancellationToken cancellationToken);
}
