namespace Aw.Judge.Infrastructure.Providers;

/// <summary>A 'choice' question: pick one of the criteria's keys (at most 255).</summary>
/// <param name="Type">Always 'choice'.</param>
/// <param name="Instructions">The prompt.</param>
/// <param name="Criteria">Each option and what it means.</param>
internal sealed record JevChoiceQuestion(string Type, string Instructions, IReadOnlyDictionary<string, string> Criteria);
