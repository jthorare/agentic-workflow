using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Pages;

/// <summary>One action on a page.</summary>
/// <param name="Id">Its id ('submit-schedule').</param>
/// <param name="Key">The action's key ('action:WeekPage.submit').</param>
/// <param name="Name">What the map shows (fenced like a concept's name).</param>
/// <param name="Mark">'owner' when the profile names it, else 'suggested'.</param>
/// <param name="Code">True when a code file implements the action.</param>
/// <param name="Concepts">The concepts it expresses, as ids without the 'concept:' prefix, in ordinal order.</param>
public sealed record PageActionRow(PageActionId Id, NodeKey Key, string Name, string Mark, bool Code, IReadOnlyList<string> Concepts);
