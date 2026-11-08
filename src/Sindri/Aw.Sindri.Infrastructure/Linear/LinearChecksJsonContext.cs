using System.Text.Json.Serialization;
using Aw.Sindri.Core.Linear;

namespace Aw.Sindri.Infrastructure.Linear;

/// <summary>The stored Linear key checks' JSON context (used through AwJson.CreateOptions).</summary>
[JsonSerializable(typeof(LinearStoredChecks))]
internal sealed partial class LinearChecksJsonContext : JsonSerializerContext;
