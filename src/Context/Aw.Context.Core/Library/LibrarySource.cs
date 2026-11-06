using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Library;

/// <summary>
/// The library record feed (PR review page spec 2026-11-04 §5.2, D13): its source id, and the one rule for whether a model may read a library record. A library root says nothing to a model unless the profile
/// opted that root in ('models: true'); every model call that lists records asks <see cref="ModelMayRead"/> first.
/// </summary>
public static class LibrarySource
{
    /// <summary>The source id of the library feed's records.</summary>
    public static SourceId Id { get; } = SourceId.From("library");

    /// <summary>Whether a model may be shown this record: any record of another source, and a library record only when its root is listed.</summary>
    /// <param name="source">The record's source.</param>
    /// <param name="key">The record's key; a library doc is 'doc:&lt;root&gt;/&lt;path&gt;'.</param>
    /// <param name="modelRoots">The library roots that opted in (<see cref="Engine.ContextSettings.LibraryModelRoots"/>).</param>
    /// <returns>True when the model may read it.</returns>
    public static bool ModelMayRead(SourceId source, RecordKey key, IReadOnlyList<string> modelRoots) => throw new NotSupportedException();
}
