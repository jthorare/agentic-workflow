namespace Aw.Judge.Core.Batching;

/// <summary>One question of a batch: <see cref="ChoiceBatchQuestion"/>, <see cref="NoulBatchQuestion"/> or <see cref="ScoreBatchQuestion"/>.</summary>
/// <param name="Key">The question's key in the request; its answer comes back under it.</param>
/// <param name="Prompt">What is asked.</param>
public abstract record BatchQuestion(string Key, string Prompt);
