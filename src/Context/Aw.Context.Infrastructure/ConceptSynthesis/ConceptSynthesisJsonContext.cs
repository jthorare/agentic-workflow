using System.Text.Json.Serialization;
using Aw.Context.Core.ConceptSynthesis;

namespace Aw.Context.Infrastructure.ConceptSynthesis;

/// <summary>The member list stored in concept_synthesis.members. Internal to Infrastructure, used only through <see cref="SqliteConceptSynthesisStore"/>.</summary>
[JsonSerializable(typeof(ConceptMember[]))]
internal sealed partial class ConceptSynthesisJsonContext : JsonSerializerContext;
