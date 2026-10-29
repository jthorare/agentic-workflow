namespace Aw.Context.Core.Sources;

/// <summary>A reference to one record in one source (spec §5.1): what a citation points at.</summary>
/// <param name="Source">The source adapter.</param>
/// <param name="Id">The record's id in that source.</param>
/// <param name="Version">Its version: blob sha (repo docs), commit sha (code), updatedAt (Linear, Slack) or map version (graph).</param>
/// <param name="Permalink">A link to it, kept only for allowed schemes and hosts (spec §7), or null.</param>
/// <param name="Canonical">The one key of the real-world record across adapters; hits sharing it merge before fusion.</param>
public sealed record SourceRef(SourceId Source, ExternalId Id, SourceVersion Version, Uri? Permalink, RecordKey Canonical);
