using System.Text.Json.Serialization;

namespace Aw.Sindri.Host.Commands.OnyxPurge;

/// <summary>The purge's own JSON context.</summary>
[JsonSerializable(typeof(PurgeState))]
internal sealed partial class PurgeJsonContext : JsonSerializerContext;
