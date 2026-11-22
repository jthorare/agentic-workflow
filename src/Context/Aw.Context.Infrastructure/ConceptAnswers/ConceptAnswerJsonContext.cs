using System.Text.Json.Serialization;
using Aw.Context.Core.Concepts;

namespace Aw.Context.Infrastructure.ConceptAnswers;

/// <summary>The answers stored in concept_answer.body (a concepts row is the answer with its rows' code paths). Internal to Infrastructure, used only through <see cref="SqliteConceptAnswerStore"/>.</summary>
[JsonSerializable(typeof(KeptConcepts))]
[JsonSerializable(typeof(ConceptGlossaryView))]
internal sealed partial class ConceptAnswerJsonContext : JsonSerializerContext;
