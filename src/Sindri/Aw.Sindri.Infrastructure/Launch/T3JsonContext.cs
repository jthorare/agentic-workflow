using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The T3 wire formats and the session map (internal, used only through AwJson.CreateOptions).</summary>
[JsonSerializable(typeof(T3Wire.RuntimeState))]
[JsonSerializable(typeof(T3Wire.IssuedSession))]
[JsonSerializable(typeof(T3Wire.Failure))]
[JsonSerializable(typeof(T3Wire.NoArgs))]
[JsonSerializable(typeof(T3Wire.Environment))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(T3Wire.Capabilities))]
[JsonSerializable(typeof(T3Wire.ProjectListArgs))]
[JsonSerializable(typeof(T3Wire.ProjectList))]
[JsonSerializable(typeof(T3Wire.LaunchArgs))]
[JsonSerializable(typeof(T3Wire.Launched))]
[JsonSerializable(typeof(T3Wire.ThreadArgs))]
[JsonSerializable(typeof(T3Wire.ThreadRead))]
[JsonSerializable(typeof(T3SessionEntry))]
internal sealed partial class T3JsonContext : JsonSerializerContext;
