using System.Text.Json.Serialization;
using Aw.Context.Core.Concepts;

namespace Aw.Context.Infrastructure.ConceptAnswers;

/// <summary>The answers stored in concept_answer.body. Internal to Infrastructure, used only through <see cref="SqliteConceptAnswerStore"/>.</summary>
[JsonSerializable(typeof(ConceptsView))]
[JsonSerializable(typeof(ConceptGlossaryView))]
internal sealed partial class ConceptAnswerJsonContext : JsonSerializerContext;
