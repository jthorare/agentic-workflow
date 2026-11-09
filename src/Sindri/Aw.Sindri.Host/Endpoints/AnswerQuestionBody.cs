namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of an answer: one of the two fields.</summary>
/// <param name="Text">A free-text answer, or null.</param>
/// <param name="Choice">One of the question's choices, or null.</param>
internal sealed record AnswerQuestionBody(string? Text, string? Choice);
