namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of an answer: one of the two fields. Both default to null: the JSON options require every constructor parameter without a default, so a body with one key would not bind.</summary>
/// <param name="Text">A free-text answer, or null.</param>
/// <param name="Choice">One of the question's choices, or null.</param>
/// <param name="Item">The item inside a 'for_each' body the answer is for (items count from 1), or null for a node outside a loop.</param>
internal sealed record AnswerQuestionBody(string? Text = null, string? Choice = null, int? Item = null);
