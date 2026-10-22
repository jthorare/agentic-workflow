using System.Text.Json.Nodes;
using Aw.Kernel.Testing;

namespace Aw.Sindri.Tests.Sandbox;

/// <summary>Reads the hand-written sandbox goldens (Golden/Sandbox) from the source tree.</summary>
internal static class SandboxGoldens
{
    internal static string Path(string name) => Golden.SourcePath(typeof(SandboxGoldens).Assembly, "Golden/Sandbox/" + name);

    internal static string Text(string name) => File.ReadAllText(Path(name));

    internal static JsonObject Object(string name) => (JsonObject)JsonNode.Parse(Text(name))!;   // why: the goldens are JSON objects
}
