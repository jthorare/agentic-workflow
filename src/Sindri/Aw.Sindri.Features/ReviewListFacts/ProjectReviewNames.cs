using Aw.Kernel;
using Aw.Sindri.Core.Projects.Cards;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Features.ReviewListFacts;

/// <summary>The project names the Reviews list's sections carry, read from the Projects page (every project, finished and hidden ones too: a review setting may name any).</summary>
/// <param name="pages">The Projects page's read.</param>
internal sealed class ProjectReviewNames(IProjectPages pages) : IReviewProjects
{
    public async ValueTask<Result<IReadOnlyDictionary<string, string>>> Names(CancellationToken cancellationToken)
    {
        var read = await pages.Read(all: true, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var page = read.Value;
        return Results.Success<IReadOnlyDictionary<string, string>>(page.Projects.Concat(page.Finished).Concat(page.Hidden).DistinctBy(static c => c.Key, StringComparer.Ordinal).ToDictionary(static c => c.Key, static c => c.Name, StringComparer.Ordinal));
    }
}
