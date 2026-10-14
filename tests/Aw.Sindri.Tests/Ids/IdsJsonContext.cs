using System.Text.Json.Serialization;

namespace Aw.Sindri.Tests.Ids;

/// <summary>The id tests' source-generated JSON context.</summary>
[JsonSerializable(typeof(ActionIdHolder))]
[JsonSerializable(typeof(CommandIdHolder))]
[JsonSerializable(typeof(CorrelationIdHolder))]
[JsonSerializable(typeof(EventIdHolder))]
[JsonSerializable(typeof(ItemAliasHolder))]
[JsonSerializable(typeof(ItemIdHolder))]
[JsonSerializable(typeof(ItemRefHolder))]
[JsonSerializable(typeof(RecordKeyHolder))]
[JsonSerializable(typeof(RepoNameHolder))]
[JsonSerializable(typeof(TraceIdHolder))]
internal sealed partial class IdsJsonContext : JsonSerializerContext;
