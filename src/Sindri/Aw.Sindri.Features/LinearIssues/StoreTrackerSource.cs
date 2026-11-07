using System.Diagnostics.CodeAnalysis;
using Aw.Kernel;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>
/// The tracker port over what the owned index stored (Linear hub spec 2026-11-04 §6.1): an issue by its key (its description and only the viewer's own comments, by author id), a project by its name from the 'linear-projects' record. Red stub; the facts ports arrive as constructor parameters with the implementation.
/// </summary>
[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "Red stub: registered when the implementation lands; Aw.Sindri.Tests uses it through InternalsVisibleTo, which MA0182 can't see.")]
internal sealed class StoreTrackerSource : ITrackerSource
{
    /// <inheritdoc/>
    public ValueTask<Result<TrackerRecord>> Read(string key, TrackerRead read, CancellationToken cancellationToken) => throw new NotSupportedException("red: StoreTrackerSource");
}
