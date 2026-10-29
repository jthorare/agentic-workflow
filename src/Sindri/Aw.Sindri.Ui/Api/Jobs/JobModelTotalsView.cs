namespace Aw.Sindri.Ui.Api.Jobs;

/// <summary>A job's model calls added up (context model routing spec §8.1); no cost, nothing reads or computes a price.</summary>
/// <param name="Calls">Calls made.</param>
/// <param name="FailedCalls">Calls no harness answered.</param>
/// <param name="InputTokens">Input tokens over all calls.</param>
/// <param name="OutputTokens">Output tokens over all calls.</param>
/// <param name="AnsweredBy">The distinct harness and model pairs that answered, in the order first seen.</param>
/// <param name="FellBack">Calls a later harness answered after an earlier one limited, failed or was skipped.</param>
internal sealed record JobModelTotalsView(int Calls, int FailedCalls, long InputTokens, long OutputTokens, IReadOnlyList<JobModelAnsweredView> AnsweredBy, int FellBack = 0);
