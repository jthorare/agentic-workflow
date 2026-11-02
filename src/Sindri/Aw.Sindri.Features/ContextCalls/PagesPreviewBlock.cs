using Aw.Context.Core.Engine;
using Aw.Context.Core.Pages;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>One page of the paste.</summary>
/// <param name="Block">Its 1-based number.</param>
/// <param name="Line">The 1-based line of its name in the paste.</param>
/// <param name="Name">The name as cleaned.</param>
/// <param name="Match">The suggested page it names, or null for a new page.</param>
/// <param name="Replaces">The id of the owner page the save would replace, or null.</param>
/// <param name="SameAs">The first earlier block that names the same page, or null.</param>
/// <param name="Ticked">False when <paramref name="Reason"/> is set.</param>
/// <param name="Reason">Why the row is not added, in one sentence, or null.</param>
/// <param name="Actions">The action lines, with the action each names and the concepts each resolved to.</param>
/// <param name="UnknownConcepts">Written concept names that name no concept, in order, once each.</param>
/// <param name="Page">The page to send to POST /sindri/context/pages in 'set', or null when the row is unticked.</param>
public sealed record PagesPreviewBlock(
    int Block, int Line, string Name, PageListMatch? Match, PageId? Replaces, int? SameAs, bool Ticked, string? Reason,
    IReadOnlyList<PageListActionRow> Actions, IReadOnlyList<string> UnknownConcepts, PageSettings? Page);
