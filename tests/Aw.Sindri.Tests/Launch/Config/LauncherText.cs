using System.Text;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Tests.Sessions;

namespace Aw.Sindri.Tests.Launch.Config;

/// <summary>Reads launcher.json text over the real built-ins, strictly (the schema's verdict) or section by section (what the launcher runs with).</summary>
internal static class LauncherText
{
    public static byte[] Bytes(string json) => Encoding.UTF8.GetBytes(json);

    public static LauncherConfig Strict(string json) => LauncherConfigFile.Read(SessionFixtures.Builtins(), Bytes(json)).Value;

    public static LauncherConfig Isolated(string json) => IsolatedLauncherRead.Read(SessionFixtures.Builtins(), Bytes(json));

    public static AgentEntry Entry(LauncherConfig config, string id) => config.Agents.Single(a => a.Id.Value == id);
}
