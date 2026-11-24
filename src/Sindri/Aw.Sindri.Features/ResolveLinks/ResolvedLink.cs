namespace Aw.Sindri.Features.ResolveLinks;

/// <summary>
/// A url's row (messages spec §9.3): its row key and fields when the local store holds the row, or 'none' (every field null) when the url has no known
/// shape or the store has no row for it. The row text came from its source and is untrusted.
/// </summary>
/// <param name="Url">The url asked for.</param>
/// <param name="RowKey">The row key ('gh:pr:owner/name#3120'), or null for 'none'.</param>
/// <param name="Title">The row's title, or null.</param>
/// <param name="State">The row's state word, or null.</param>
public sealed record ResolvedLink(Uri Url, string? RowKey, string? Title, string? State)
{
    /// <summary>Gets the row's CI state word, or null.</summary>
    public string? Ci { get; init; }

    /// <summary>Gets who has the row's next move, or null.</summary>
    public string? WaitsOn { get; init; }

    /// <summary>Gets when the row started to need the owner (its age), or null.</summary>
    public DateTimeOffset? Since { get; init; }
}
