using System.Text.Json.Serialization;

namespace Aw.Forge.Infrastructure.World;

/// <summary>The one document the file forge stores: serialised through this context so it works under NativeAOT.</summary>
[JsonSerializable(typeof(WorldState))]
internal sealed partial class ForgeFileJsonContext : JsonSerializerContext;
