using System.Text.Json.Serialization;
using Aw.JevWire;

namespace Aw.Sindri.Infrastructure.Workflow.Jev;

/// <summary>The source-generated JSON for Jev's batch wire records; used only through <see cref="JevClient"/>'s AwJson options.</summary>
[JsonSerializable(typeof(JevBatchRequest))]
[JsonSerializable(typeof(JevBatchResponse))]
internal sealed partial class JevClientJsonContext : JsonSerializerContext;
