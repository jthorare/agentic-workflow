namespace Aw.Context.Core.Concepts;

/// <summary>One page of a concepts read: where to resume and how many to answer. There is no upper limit on <paramref name="Limit"/>; a read that wants everything pages until 'next' is gone.</summary>
/// <param name="Cursor">The 'next' of the previous page (opaque), or null for the first page; one from another map version is SND-CONTEXT-006.</param>
/// <param name="Limit">The most entries (at least 1), or null for 100.</param>
public sealed record ConceptPage(string? Cursor = null, int? Limit = null);
