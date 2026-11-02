using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Records;

/// <summary>Who the owner is in one source: the user its credential belongs to (the 'source_identity' row).</summary>
/// <param name="Source">The source, such as 'linear'.</param>
/// <param name="UserRef">The user's record reference, such as 'linear:user/&lt;id&gt;': what a 'led-by' link points at.</param>
/// <param name="Name">The user's display name.</param>
/// <param name="ReadAt">When the source was asked.</param>
public sealed record SourceIdentity(SourceId Source, string UserRef, string Name, DateTimeOffset ReadAt);
