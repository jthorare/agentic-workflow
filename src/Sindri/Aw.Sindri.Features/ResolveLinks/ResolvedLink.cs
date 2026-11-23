namespace Aw.Sindri.Features.ResolveLinks;

/// <summary>A url's row (messages spec §9.3): its row key and fields, or 'none'.</summary>
/// <param name="Url">The url asked for.</param>
/// <param name="RowKey">The row key ('github:pr:owner/name#3120'), or null for 'none'.</param>
/// <param name="Title">The row's title, or null.</param>
/// <param name="State">The row's state word, or null.</param>
public sealed record ResolvedLink(Uri Url, string? RowKey, string? Title, string? State);
