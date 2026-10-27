using Aw.Judge.Core.Ids;

namespace Aw.Judge.Core.Labels;

/// <summary>One thing a question can be scored on (the 'eval_items' table): a stored input, where it came from and what the judge decided.</summary>
/// <param name="Id">The item id.</param>
/// <param name="Question">The question the input belongs to, e.g. 'ask-check'.</param>
/// <param name="InputJson">The redacted, capped input as JSON.</param>
/// <param name="Source">Where the item came from, 'decision:&lt;decision id&gt;' or 'transcript:&lt;session&gt;:&lt;turn&gt;'; unique, so an import adds an item once.</param>
/// <param name="ModelDecision">What the judge decided, or null for an item made from a transcript.</param>
/// <param name="CreatedAt">When the item was imported.</param>
public sealed record EvalItem(EvalItemId Id, string Question, string InputJson, string Source, string? ModelDecision, DateTimeOffset CreatedAt);
