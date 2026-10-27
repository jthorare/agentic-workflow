using System.Text.Json.Serialization;
using Aw.Context.Core.ConceptScan;

namespace Aw.Context.Infrastructure.ConceptScan;

/// <summary>The hint list stored in map_concept_hint.hints. Internal to Infrastructure, used only through <see cref="SqliteConceptHintStore"/>.</summary>
[JsonSerializable(typeof(ConceptHint[]))]
internal sealed partial class ConceptHintJsonContext : JsonSerializerContext;
