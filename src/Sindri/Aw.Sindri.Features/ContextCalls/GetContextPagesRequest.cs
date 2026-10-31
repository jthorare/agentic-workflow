using Aw.Context.Core.Pages;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>One slice of the product's pages with their actions and concepts (GET /sindri/context/pages).</summary>
/// <param name="Repo">Keeps the pages of this repo, or null for every repo the caller may read.</param>
/// <param name="Concept">Keeps the pages that express this concept (an id or 'concept:id') or have an action that does, or null for all.</param>
/// <param name="Page">Keeps the one page with this id, or null for all.</param>
/// <param name="Suggested">True for the suggested pages only; false for the owner's named pages.</param>
/// <param name="Limit">Pages per answer (50 when null; 1 to 200).</param>
/// <param name="Offset">Pages to skip (0 when null).</param>
/// <param name="Draft">True to read the draft map.</param>
/// <param name="MapVersion">A retained map version ('v3'), or null for the current one; not valid with <paramref name="Draft"/>.</param>
public sealed record GetContextPagesRequest(string? Repo = null, string? Concept = null, string? Page = null, bool Suggested = false, int? Limit = null, int? Offset = null, bool Draft = false, string? MapVersion = null) : IQuery<PagesView>;
