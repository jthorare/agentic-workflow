using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.SetPages;

/// <summary>What a SetPages request does to the live profile, worked out and checked before anything is written.</summary>
/// <param name="Edits">The writes, sets first and then removals.</param>
/// <param name="Pages">Each page touched, in request order.</param>
/// <param name="Diff">The page rows that change between the live profile and the planned one.</param>
internal sealed record PagePlan(IReadOnlyList<PageEdit> Edits, IReadOnlyList<PageWritten> Pages, IReadOnlyList<string> Diff);
