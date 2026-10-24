using System.Text.Json.Serialization;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>A question of a batched /v1/systemone request; the wire's 'type' picks the kind.</summary>
/// <param name="Instructions">The prompt.</param>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(JevBatchChoice), "choice")]
[JsonDerivedType(typeof(JevBatchNoul), "noul")]
[JsonDerivedType(typeof(JevBatchScore), "score")]
internal abstract record JevBatchQuestion(string Instructions);
