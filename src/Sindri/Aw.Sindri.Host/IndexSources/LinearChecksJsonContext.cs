using System.Text.Json.Serialization;
using Aw.Sindri.Features.LinearConnection;

namespace Aw.Sindri.Host.IndexSources;

/// <summary>The stored Linear key checks' JSON context (used through AwJson.CreateOptions).</summary>
[JsonSerializable(typeof(LinearStoredChecks))]
internal sealed partial class LinearChecksJsonContext : JsonSerializerContext;
