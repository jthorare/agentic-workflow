using Aw.Context.Core.Sources;

namespace Aw.Context.Infrastructure.Docs;

/// <summary>What one repo's pull has to do: the documents to hand over (path, blob, and the packet.json blob beside it) and the stored keys whose documents left the tree or the globs.</summary>
/// <param name="Changed">The documents new or changed since the store last saw them (or cleaned under another ruleset), in path order.</param>
/// <param name="Removed">The live stored records of the repo with no document at its head.</param>
internal sealed record RepoDocsPlan(IReadOnlyList<RepoDocChange> Changed, IReadOnlyList<RecordKey> Removed);
