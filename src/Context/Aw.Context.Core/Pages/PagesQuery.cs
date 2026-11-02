using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Pages;

/// <summary>The pages read: a filter, the page and which map.</summary>
/// <param name="Concept">Keeps the pages that express this concept or have an action that does, or null for all.</param>
/// <param name="Page">Keeps the one page with this id or map key (owner or suggested), or null for all.</param>
/// <param name="Repo">Keeps the pages of this repo, or null for every repo the caller may read.</param>
/// <param name="Marks">Which pages: owner, suggested or both.</param>
/// <param name="Limit">Pages per answer (50 when null; 1 to 200).</param>
/// <param name="Offset">Pages to skip (0 when null).</param>
/// <param name="Draft">True to read the draft map.</param>
/// <param name="MapVersion">A retained map version ('v3'), or null for the current one; not valid with <paramref name="Draft"/>.</param>
public sealed record PagesQuery(ConceptId? Concept, string? Page, ContextRepoName? Repo, PageMarks Marks, int? Limit, int? Offset, bool Draft, string? MapVersion)
{
    /// <summary>Keeps the pages whose name or map label contains this text, ignoring case (the sheet's Pick another, pages spec §3.6); null or blank keeps all.</summary>
    public string? Q { get; init; }
}
