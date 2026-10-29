using System.Security.Cryptography;
using System.Text;

namespace Aw.Sindri.Core.Launch;

/// <summary>The cache key of one prepared directory (sandbox spec §3.8).</summary>
public static class PrepareKey
{
    /// <summary>The key: SHA-256 of the lockfile as the base commit has it and the packages allowed to run install scripts (they change what an install leaves behind).</summary>
    /// <param name="lockfile">The lockfile's text at the base commit.</param>
    /// <param name="allowScripts">The packages whose install scripts run.</param>
    /// <returns>Lowercase hex.</returns>
    public static string Of(string lockfile, IReadOnlyList<string> allowScripts)
    {
        ArgumentNullException.ThrowIfNull(lockfile);
        ArgumentNullException.ThrowIfNull(allowScripts);
        var text = lockfile + "\n--allow-scripts--\n" + string.Join('\n', allowScripts.Order(StringComparer.Ordinal));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
}
