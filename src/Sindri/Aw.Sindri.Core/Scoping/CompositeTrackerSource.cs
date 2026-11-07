using System.Diagnostics.CodeAnalysis;
using Aw.Kernel;

namespace Aw.Sindri.Core.Scoping;

/// <summary>
/// The one <see cref="ITrackerSource"/> the host registers (Linear hub spec 2026-11-04 §6.1): the file source when AW_SINDRI_TRACKER_DIR is set, else the store source. Red stub; the sources arrive as constructor parameters with the implementation.
/// </summary>
[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "Red stub: registered when the implementation lands; Aw.Sindri.Tests uses it through InternalsVisibleTo, which MA0182 can't see.")]
internal sealed class CompositeTrackerSource : ITrackerSource
{
    /// <inheritdoc/>
    public ValueTask<Result<TrackerRecord>> Read(string key, TrackerRead read, CancellationToken cancellationToken) => throw new NotSupportedException("red: CompositeTrackerSource");
}
