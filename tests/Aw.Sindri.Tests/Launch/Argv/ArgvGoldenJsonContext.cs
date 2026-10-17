using System.Text.Json.Serialization;

namespace Aw.Sindri.Tests.Launch.Argv;

[JsonSerializable(typeof(ArgvGolden))]
internal sealed partial class ArgvGoldenJsonContext : JsonSerializerContext;
