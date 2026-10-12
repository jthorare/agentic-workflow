namespace Aw.Judge.Infrastructure.Providers;

/// <summary>POST /v1/systemone's response: an answer per question id (usage and other fields are ignored).</summary>
/// <param name="Answers">The answers.</param>
internal sealed record JevResponse(IReadOnlyDictionary<string, JevAnswer>? Answers = null);
