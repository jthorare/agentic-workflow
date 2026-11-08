using Aw.Kernel;

namespace Aw.Sindri.Core.Connections;

/// <summary>Reads a preset file.</summary>
public static class ConnectionPresets
{
    /// <summary>Parses one preset's JSON (camelCase keys: id (read as Key), displayName, kind, authorizeUrl, tokenUrl, revokeUrl, defaultScopes, hostGroup, loopbackAllowed, signInNote, pollableEvents, probeUrl).</summary>
    /// <param name="json">The file's text.</param>
    /// <returns>The preset, or invalid input naming the key that is wrong.</returns>
    public static Result<ConnectionPreset> Parse(string json) => throw new NotSupportedException();
}
