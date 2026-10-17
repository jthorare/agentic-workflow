using System.Text.Json.Serialization;

namespace Aw.Sindri.Tests.Launch.Forms;

[JsonSerializable(typeof(ArgvStepGolden))]
internal sealed partial class ArgvStepGoldenJsonContext : JsonSerializerContext;
