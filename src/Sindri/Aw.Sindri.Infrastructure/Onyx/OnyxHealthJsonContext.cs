using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>This adapter's own JSON context for Onyx's reply, whose record is internal (used only through AwJson.CreateOptions).</summary>
[JsonSerializable(typeof(OnyxVersionWire))]
internal sealed partial class OnyxHealthJsonContext : JsonSerializerContext;
