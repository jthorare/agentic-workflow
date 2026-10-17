using System.Text.Json.Serialization;

namespace Aw.Sindri.Tests.Ids;

/// <summary>The id tests' source-generated JSON context.</summary>
[JsonSerializable(typeof(ActionIdHolder))]
[JsonSerializable(typeof(Launch.AgentIdHolder))]
[JsonSerializable(typeof(CommandIdHolder))]
[JsonSerializable(typeof(CorrelationIdHolder))]
[JsonSerializable(typeof(DecisionIdHolder))]
[JsonSerializable(typeof(DecisionOptionHolder))]
[JsonSerializable(typeof(EventIdHolder))]
[JsonSerializable(typeof(ItemAliasHolder))]
[JsonSerializable(typeof(JobIdHolder))]
[JsonSerializable(typeof(JobKeyHolder))]
[JsonSerializable(typeof(JobKindHolder))]
[JsonSerializable(typeof(ItemIdHolder))]
[JsonSerializable(typeof(ItemRefHolder))]
[JsonSerializable(typeof(NeedsYouRowIdHolder))]
[JsonSerializable(typeof(RecordKeyHolder))]
[JsonSerializable(typeof(RequestIdHolder))]
[JsonSerializable(typeof(RepoNameHolder))]
[JsonSerializable(typeof(SubjectIdHolder))]
[JsonSerializable(typeof(TraceIdHolder))]
internal sealed partial class IdsJsonContext : JsonSerializerContext;
