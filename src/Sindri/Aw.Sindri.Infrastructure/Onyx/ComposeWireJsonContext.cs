using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>This adapter's own JSON context for Docker's wire formats, whose records are internal (used only through AwJson.CreateOptions).</summary>
[JsonSerializable(typeof(ComposePsRow))]
[JsonSerializable(typeof(List<ComposePsRow>))]
[JsonSerializable(typeof(ComposeConfigWire))]
[JsonSerializable(typeof(List<string>))]
internal sealed partial class ComposeWireJsonContext : JsonSerializerContext;
