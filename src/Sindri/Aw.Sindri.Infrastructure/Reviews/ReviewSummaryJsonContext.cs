using System.Text.Json.Serialization;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Infrastructure.Reviews;

/// <summary>The summary file's JSON context (used through AwJson.CreateOptions).</summary>
[JsonSerializable(typeof(ReviewSummary))]
internal sealed partial class ReviewSummaryJsonContext : JsonSerializerContext;
