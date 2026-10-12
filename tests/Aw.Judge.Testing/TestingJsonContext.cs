using System.Text.Json.Serialization;

namespace Aw.Judge.Testing;

/// <summary>The test question's source-generated JSON context, standing in for the package's (which holds the real input records).</summary>
[JsonSerializable(typeof(TextInput))]
internal sealed partial class TestingJsonContext : JsonSerializerContext;
