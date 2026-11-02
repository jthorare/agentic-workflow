using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Sessions;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>Reads the Linux box for the plan builder (Core reads no environment): home, state directory, the installed C# build, uid, RAM, cores, the environment and the other homes.</summary>
internal static class BoxHostFacts
{
    /// <summary>Reads the facts from the box.</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="sindriHome">The real AW_SINDRI_HOME.</param>
    /// <param name="machine">The machine's memory and cores (the one source doctor and the scope limits share).</param>
    /// <returns>The facts.</returns>
    internal static SandboxHostFacts Read(Func<string, string?> environment, string sindriHome, ISandboxMachine machine)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(machine);
        var home = environment("HOME") is { Length: > 0 } configured ? configured : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var state = StateDir(environment, home);
        return new SandboxHostFacts(home, state, sindriHome, CsBinDir(state), OwnUid.Read(), machine.RamBytes, machine.Cores, IsMacOs: false, ProcessEnvironment(), OtherHomes(Path.GetDirectoryName(home) ?? "/home", home));
    }

    /// <summary>Gets AW_STATE_DIR, or ~/.agentic-workflow.</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="home">The owner's home.</param>
    /// <returns>The directory.</returns>
    internal static string StateDir(Func<string, string?> environment, string home) =>
        environment("AW_STATE_DIR") is { Length: > 0 } state ? state : Path.Combine(home, ".agentic-workflow");

    /// <summary>Resolves bin/cs/current to its stamp directory under the state directory.</summary>
    /// <param name="stateDir">AW_STATE_DIR.</param>
    /// <returns>'&lt;state&gt;/bin/cs/&lt;stamp&gt;', or empty when none is installed.</returns>
    internal static string CsBinDir(string stateDir)
    {
        var current = new DirectoryInfo(Path.Combine(stateDir, "bin", "cs", "current"));
        return current.Exists && current.ResolveLinkTarget(returnFinalTarget: true) is { Exists: true } target ? Path.Combine(stateDir, "bin", "cs", target.Name) : string.Empty;
    }

    /// <summary>Finds the other users' homes beside the owner's.</summary>
    /// <param name="homesRoot">The directory that holds the homes (the owner's home's parent).</param>
    /// <param name="ownHome">The owner's home.</param>
    /// <returns>The other homes, sorted.</returns>
    internal static IReadOnlyList<string> OtherHomes(string homesRoot, string ownHome)
    {
        if (!Directory.Exists(homesRoot))
        {
            return [];
        }

        return [.. Directory.EnumerateDirectories(homesRoot).Where(d => !string.Equals(d, ownHome, StringComparison.Ordinal)).Order(StringComparer.Ordinal)];
    }

    private static Dictionary<string, string> ProcessEnvironment()
    {
        var all = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key && entry.Value is string value)
            {
                all[key] = value;
            }
        }

        return all;
    }
}
