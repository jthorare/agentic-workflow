namespace Aw.Context.Core.Engine;

/// <summary>An action of a page the owner named in the profile's 'index.pages' (pages spec §3.1).</summary>
/// <param name="Id">Its id, unique within the page.</param>
/// <param name="Name">What the map shows.</param>
/// <param name="ActionKey">The map action it names ('action:SchedulePage.submit-schedule'), or null for an owner action.</param>
/// <param name="Concepts">Ids of the pins, or 'concept:&lt;id&gt;' keys of derived concepts, it relates to.</param>
public sealed record PageActionPin(PageActionId Id, string Name, string? ActionKey, IReadOnlyList<string> Concepts);
