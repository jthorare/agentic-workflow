namespace Aw.Judge.Infrastructure.Providers;

/// <summary>A 'choice' question: pick one of the criteria's keys (at most 255).</summary>
/// <param name="Instructions">The prompt.</param>
/// <param name="Criteria">Each option and what it means.</param>
internal sealed record JevBatchChoice(string Instructions, IReadOnlyDictionary<string, string> Criteria) : JevBatchQuestion(Instructions);
