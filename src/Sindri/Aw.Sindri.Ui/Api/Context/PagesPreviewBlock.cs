namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One page of the paste, as the sheet's row.</summary>
/// <param name="Block">Its 1-based number ('Same page as block 2' says these).</param>
/// <param name="Line">The 1-based line of its name in the paste.</param>
/// <param name="Name">The name as cleaned.</param>
/// <param name="Match">The suggested page it names; null for a new page.</param>
/// <param name="Replaces">The id of the owner page the save would replace; null when it replaces none.</param>
/// <param name="SameAs">The first earlier block that names the same page; null when none does.</param>
/// <param name="Ticked">Whether the row starts ticked; false when <paramref name="Reason"/> is set.</param>
/// <param name="Reason">Why the row is not added, in one sentence; null when it is.</param>
/// <param name="Actions">The action lines.</param>
/// <param name="UnknownConcepts">Written concept names that name no concept, in order.</param>
/// <param name="Page">The page to send in 'set' of POST /sindri/context/pages; null when the row is not ticked.</param>
internal sealed record PagesPreviewBlock(
    int Block, int Line, string Name, PagesPreviewMatch? Match, string? Replaces, int? SameAs, bool Ticked, string? Reason,
    IReadOnlyList<PagesPreviewAction> Actions, IReadOnlyList<string> UnknownConcepts, PagesPreviewPage? Page);
