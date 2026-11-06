using Aw.Context.Core.Sources;

namespace Aw.Context.Features.LinearWriteThrough;

/// <summary>What the write-through stored.</summary>
/// <param name="Key">The issue's record key, or null when Linear no longer has the issue or it is not in one of the profile's teams (nothing was stored).</param>
/// <param name="Written">Whether the store changed: false when the stored record was already current or a newer one was stored.</param>
internal sealed record LinearWriteThroughResponse(RecordKey? Key, bool Written);
