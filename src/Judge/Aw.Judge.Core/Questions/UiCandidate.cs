using System.Text.Json.Serialization;

namespace Aw.Judge.Core.Questions;

/// <summary>One element on the page a ui-element-repair may pick.</summary>
/// <param name="Index">The element's index, which a 'repaired' answer returns as chosenIndex.</param>
/// <param name="Role">Its ARIA role, or null.</param>
/// <param name="AccessibleName">Its accessible name, or null.</param>
/// <param name="TestIdAttribute">Its data-testid (JSON 'testId'), or null.</param>
/// <param name="Text">Its visible text, or null.</param>
public sealed record UiCandidate(
    int Index,
    string? Role,
    string? AccessibleName,
    [property: JsonPropertyName("testId")] string? TestIdAttribute,
    string? Text);
