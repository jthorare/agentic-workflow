namespace Aw.Judge.Core.Batching;

/// <summary>Pick one option (at most 255).</summary>
/// <param name="Key">The question's key.</param>
/// <param name="Prompt">What is asked.</param>
/// <param name="Criteria">Each option and what it means.</param>
public sealed record ChoiceBatchQuestion(string Key, string Prompt, IReadOnlyDictionary<string, string> Criteria) : BatchQuestion(Key, Prompt);
