using System.Text.Json.Serialization;

namespace Aw.Judge.Tests.Ids;

/// <summary>The id tests' source-generated JSON context.</summary>
[JsonSerializable(typeof(JudgeIdsHolder))]
internal sealed partial class IdsJsonContext : JsonSerializerContext;
