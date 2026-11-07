using System.Security.Cryptography;
using System.Text;

namespace Aw.Sindri.Core.Reviews;

/// <summary>A PR's head branch as the fix-task files name it: "owner/repo:branch" with owner and repo lowercased (GitHub names are case-insensitive) and the branch kept as is.</summary>
public static class FixHead
{
    /// <summary>The head string.</summary>
    /// <param name="repo">The head repository as "owner/name".</param>
    /// <param name="branch">The head branch.</param>
    /// <returns>"owner/name:branch", owner and name lowercased.</returns>
    public static string Of(string repo, string branch)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(branch);
        return repo.ToLowerInvariant() + ":" + branch;
    }

    /// <summary>The name of the head's file, without the folder: the lowercase sha256 hex of the head string's UTF-8 bytes, then ".json".</summary>
    /// <param name="head">The head string (see <see cref="Of"/>).</param>
    /// <returns>The file name.</returns>
    public static string FileName(string head)
    {
        ArgumentNullException.ThrowIfNull(head);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(head))) + ".json";
    }
}
