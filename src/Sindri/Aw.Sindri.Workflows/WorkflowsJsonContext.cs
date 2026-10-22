using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The adapter's own JSON context for the stored content it reads in activities (used only through AwJson.CreateOptions).</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ItemFact))]
[JsonSerializable(typeof(RegisteredEventPayload))]
[JsonSerializable(typeof(PrWatchFile))]   // the pr-watch poller's state file
[JsonSerializable(typeof(FactValue))]   // engine P6 unit (a): a fact as an effect argument
internal sealed partial class WorkflowsJsonContext : JsonSerializerContext;
