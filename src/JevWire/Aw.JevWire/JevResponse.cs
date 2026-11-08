namespace Aw.JevWire;

/// <summary>POST /v1/systemone's response: an answer per question id (usage and other fields are ignored).</summary>
/// <param name="Answers">The answers.</param>
public sealed record JevResponse(IReadOnlyDictionary<string, JevAnswer>? Answers = null);
