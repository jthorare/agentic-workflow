using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Setup;

/// <summary>The key of a RegisterMcp job (setup-once spec §5.4).</summary>
public static class McpJobKeys
{
    /// <summary>'mcp-' and the first 16 hex of the sha256 of the repo's name (the name is never in the key: it allows capitals and 100 characters).</summary>
    /// <param name="repo">The repo.</param>
    /// <returns>The key.</returns>
    public static JobKey For(RepoName repo) => JobKey.From("mcp-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(repo.Value)))[..16]);
}
