namespace Aw.Sindri.Features.PickTodoHold;

/// <summary>What a hold pick answers (todo spec §4.4).</summary>
/// <param name="Kept">'applied' or 'restored'.</param>
/// <param name="Count">How many held changes the pick covered.</param>
/// <param name="UndoUntil">Until when the pick can be undone (10 seconds on Sindri's clock).</param>
public sealed record PickTodoHoldResponse(string Kept, int Count, DateTimeOffset UndoUntil);
