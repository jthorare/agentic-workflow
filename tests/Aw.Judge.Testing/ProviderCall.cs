using System.Text.Json.Nodes;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Testing;

/// <summary>One call a <see cref="ScriptedProvider"/> received.</summary>
/// <param name="Question">The question.</param>
/// <param name="Input">The input.</param>
/// <param name="Budget">The time budget.</param>
public sealed record ProviderCall(QuestionRef Question, JsonNode? Input, TimeSpan Budget);
