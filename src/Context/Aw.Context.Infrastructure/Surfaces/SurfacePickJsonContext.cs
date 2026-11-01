using System.Text.Json.Serialization;

namespace Aw.Context.Infrastructure.Surfaces;

/// <summary>The node-key list stored in surface_pick.candidate_keys. Internal to Infrastructure, used only through <see cref="SqliteSurfacePickStore"/>.</summary>
[JsonSerializable(typeof(string[]))]
internal sealed partial class SurfacePickJsonContext : JsonSerializerContext;
