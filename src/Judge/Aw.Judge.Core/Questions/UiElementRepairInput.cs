namespace Aw.Judge.Core.Questions;

/// <summary>ui-element-repair's input: a Playwright step whose selector broke, and the elements on the page then.</summary>
/// <param name="BrokenSelector">The selector that found nothing.</param>
/// <param name="Step">The step's description.</param>
/// <param name="Candidates">The elements to choose from.</param>
public sealed record UiElementRepairInput(string BrokenSelector, string Step, IReadOnlyList<UiCandidate> Candidates);
