namespace Aw.Judge.Infrastructure.Providers;

/// <summary>A 'score' question: pick a level of a scale of at most 10, lowest first.</summary>
/// <param name="Instructions">The prompt.</param>
/// <param name="Criteria">What each level means, lowest first.</param>
internal sealed record JevBatchScore(string Instructions, IReadOnlyList<string> Criteria) : JevBatchQuestion(Instructions);
