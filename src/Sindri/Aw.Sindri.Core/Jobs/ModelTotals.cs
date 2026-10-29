namespace Aw.Sindri.Core.Jobs;

/// <summary>A job's model calls added up (<see cref="JobRow.Models"/>), folded from its 'context.model.called' events. No cost: nothing reads or computes a price.</summary>
/// <param name="Calls">Calls made.</param>
/// <param name="FailedCalls">Calls no harness answered.</param>
/// <param name="InputTokens">Input tokens over all calls (reported or estimated per call).</param>
/// <param name="OutputTokens">Output tokens over all calls.</param>
/// <param name="AnsweredBy">The distinct harness and model pairs that answered, in the order first seen, with counts.</param>
public sealed record ModelTotals(int Calls, int FailedCalls, long InputTokens, long OutputTokens, IReadOnlyList<ModelAnsweredCount> AnsweredBy);
