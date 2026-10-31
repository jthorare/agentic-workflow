using System.Globalization;
using Aw.Sindri.Infrastructure.Sessions;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>Tells whether this user owns a folder (macOS, through 'stat -f %u': .NET has no owner call).</summary>
internal static class MacTempOwner
{
    /// <summary>Asks stat.</summary>
    /// <param name="path">The folder.</param>
    /// <returns>True when its owner is this user; false when stat fails.</returns>
    internal static bool Owns(string path)
    {
        var run = new SandboxProcesses().Run("/usr/bin/stat", ["-f", "%u", path], TimeSpan.FromSeconds(5));
        return run.Succeeded && int.TryParse(run.Output.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var uid) && uid == OwnUid.Read();
    }
}
