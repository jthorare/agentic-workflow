using Aw.Context.Core.Library;

namespace Aw.Context.Infrastructure.Docs;

/// <summary>A near miss as the status file holds it; its own type so <see cref="LibraryNearMissConverter"/> doesn't call itself.</summary>
/// <param name="Path">The root-relative path.</param>
/// <param name="Error">The SND-LIB-019 error.</param>
internal sealed record StoredNearMiss(string Path, DesignDocError Error);
