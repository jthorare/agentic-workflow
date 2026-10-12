using System.Text.Json.Nodes;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>POST /v1/systemone's body: the state (the question's input), the model, and the questions by id.</summary>
/// <param name="State">The input.</param>
/// <param name="Model">The Jev model.</param>
/// <param name="Questions">The questions; the judge asks one, 'decision'.</param>
internal sealed record JevRequest(JsonNode? State, string Model, IReadOnlyDictionary<string, JevChoiceQuestion> Questions);
