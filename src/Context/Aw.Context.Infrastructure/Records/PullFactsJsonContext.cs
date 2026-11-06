using System.Text.Json.Serialization;
using Aw.Context.Core.Records;

namespace Aw.Context.Infrastructure.Records;

/// <summary>The JSON a pull request's facts are stored as, in 'source_record.pull_facts'. Internal to Infrastructure.</summary>
[JsonSerializable(typeof(PullFacts))]
internal sealed partial class PullFactsJsonContext : JsonSerializerContext;
