using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Library;

/// <summary>What the scope checks (SND-LIB-015, -016) need of one stored doc besides its <see cref="LibraryDoc"/> row: its parent and supersedes, its item ids and its refs.</summary>
/// <param name="Doc">The doc.</param>
/// <param name="Parent">The front matter's parent as written.</param>
/// <param name="Supersedes">The front matter's supersedes as written.</param>
/// <param name="Items">The doc's items in document order (key, kind and parent; no text or lines).</param>
/// <param name="Refs">The doc's refs as stored (no line).</param>
internal sealed record LibraryScopeFacts(RecordKey Doc, string? Parent, string? Supersedes, IReadOnlyList<DesignItem> Items, IReadOnlyList<DesignRef> Refs);
