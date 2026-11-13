using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Tests.StartSession;

internal sealed class FakeEnvironment : IEnvironmentReader
{
    public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal)
    {
        ["HOME"] = "/home/thor",
        ["PATH"] = "/usr/bin",
        ["TERM"] = "xterm",
        ["AW_STATE_DIR"] = "/state",
        ["AW_FLEET_DIR"] = "/state/fleet",
        ["AW_SINDRI_TOKEN"] = "secret",
        ["GH_TOKEN"] = "gh-secret",
        ["ANTHROPIC_API_KEY"] = "sk-a",
        ["OPENAI_API_KEY"] = "sk-o",
    };

    public string? Get(string name) => Values.GetValueOrDefault(name);
}
