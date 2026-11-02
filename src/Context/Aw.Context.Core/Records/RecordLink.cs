using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Records;

/// <summary>One link a source states from a record (spec §4.2).</summary>
/// <param name="ToRef">The other record's key when known, 'path:&lt;repo&gt;/&lt;path&gt;' for a file, else the url as the source gave it (scrubbed).</param>
/// <param name="Kind">What the link says.</param>
/// <param name="Title">The other record's title, when the source gave it.</param>
/// <param name="ToKey">The stored record <paramref name="ToRef"/> resolves to; the store fills it.</param>
public sealed record RecordLink(string ToRef, RecordLinkKind Kind, string? Title, RecordKey? ToKey = null);
