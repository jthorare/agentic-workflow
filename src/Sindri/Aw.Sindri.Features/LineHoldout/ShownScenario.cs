namespace Aw.Sindri.Features.LineHoldout;

/// <summary>A hidden scenario as the owner reads it.</summary>
/// <param name="Id">The scenario's id.</param>
/// <param name="Item">The visible acceptance item it is tied to.</param>
/// <param name="Kind">What runs it: api, ui, db and so on.</param>
/// <param name="Steps">What the evaluator does.</param>
/// <param name="Expected">What should happen.</param>
/// <param name="WasSpent">Whether it was already spent before this read.</param>
internal sealed record ShownScenario(string Id, string? Item, string Kind, IReadOnlyList<string> Steps, string Expected, bool WasSpent);
