namespace Aw.Judge.Core.Batching;

/// <summary>Pick a level of a scale (at most 10), lowest first.</summary>
/// <param name="Key">The question's key.</param>
/// <param name="Prompt">What is asked.</param>
/// <param name="Levels">What each level means, lowest first.</param>
public sealed record ScoreBatchQuestion(string Key, string Prompt, IReadOnlyList<string> Levels) : BatchQuestion(Key, Prompt);
