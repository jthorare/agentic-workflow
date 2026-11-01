namespace Aw.Sindri.Ui.Api.Context;

/// <summary>What GET /sindri/context/pages is asked (pages spec §3.1).</summary>
/// <param name="Repo">The repo to read; null for the profile's only repo.</param>
/// <param name="Concept">Only the pages with an action on this concept; null for all.</param>
/// <param name="Limit">The slice size; null leaves it to the server.</param>
/// <param name="Offset">How many pages to skip; null for the first slice.</param>
/// <param name="Marks">Which pages to keep: "owner", "suggested" or "all"; null leaves it to the server (all).</param>
internal sealed record PagesQuery(string? Repo = null, string? Concept = null, int? Limit = null, int? Offset = null, string? Marks = null)
{
    /// <summary>Gets the text a page's name or map label must contain (any case); null or blank keeps every page.</summary>
    public string? Q { get; init; }
}
