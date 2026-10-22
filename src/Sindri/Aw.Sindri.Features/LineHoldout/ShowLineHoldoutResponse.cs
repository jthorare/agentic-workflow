namespace Aw.Sindri.Features.LineHoldout;

/// <summary>What a show answered.</summary>
/// <param name="Line">The line's slug.</param>
/// <param name="Phase">The phase key.</param>
/// <param name="SetHash">The hash of the set file.</param>
/// <param name="Total">The hidden scenarios, the canary not counted.</param>
/// <param name="Spent">How many of them are spent already.</param>
/// <param name="Scenarios">The scenarios with their text after the confirmation; null in the preview.</param>
/// <param name="Challenge">The preview's challenge; null after the confirmation.</param>
internal sealed record ShowLineHoldoutResponse(string Line, string Phase, string SetHash, int Total, int Spent, IReadOnlyList<ShownScenario>? Scenarios, HoldoutChallenge? Challenge);
