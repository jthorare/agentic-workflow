namespace Aw.Judge.Core.Batching;

/// <summary>A yes/no question answered as a number (the model's own scale, read by the caller).</summary>
/// <param name="Key">The question's key.</param>
/// <param name="Prompt">What is asked.</param>
public sealed record NoulBatchQuestion(string Key, string Prompt) : BatchQuestion(Key, Prompt);
