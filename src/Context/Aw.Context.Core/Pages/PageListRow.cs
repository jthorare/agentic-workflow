using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Pages;

/// <summary>One previewed block: what it matched, whether the save would add it, and the page it would save.</summary>
/// <param name="Block">The block's number.</param>
/// <param name="Line">The line of its name in the paste.</param>
/// <param name="Name">The name as cleaned.</param>
/// <param name="Match">The suggested page it names, or null for a new page.</param>
/// <param name="Replaces">The id of the owner page the save would replace, or null.</param>
/// <param name="SameAs">The first earlier block that names the same page, or null.</param>
/// <param name="Ticked">False when <paramref name="Reason"/> is set.</param>
/// <param name="Reason">The one sentence for an unticked row (the first problem), or null.</param>
/// <param name="Actions">The action lines.</param>
/// <param name="UnknownConcepts">Written concept names with no id, in order, once each.</param>
/// <param name="Page">The page the save would set, or null when unticked.</param>
public sealed record PageListRow(
    int Block, int Line, string Name, PageListMatch? Match, PageId? Replaces, int? SameAs, bool Ticked, string? Reason,
    IReadOnlyList<PageListActionRow> Actions, IReadOnlyList<string> UnknownConcepts, PagePin? Page);
