using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Records;

/// <summary>Which stored records a listing returns; every field narrows, null does not.</summary>
/// <param name="Source">Only this source.</param>
/// <param name="Repo">Only this repo.</param>
/// <param name="Kind">Only this kind.</param>
/// <param name="IncludeRemoved">Also tombstones.</param>
public sealed record RecordFilter(SourceId? Source = null, ContextRepoName? Repo = null, RecordKind? Kind = null, bool IncludeRemoved = false);
