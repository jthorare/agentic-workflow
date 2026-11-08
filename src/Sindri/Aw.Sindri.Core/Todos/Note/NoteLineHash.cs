using System.Security.Cryptography;
using System.Text;

namespace Aw.Sindri.Core.Todos.Note;

/// <summary>The hash that tells a stale copy of a removed todo's line from an owner re-add (todo spec §4.1 'lastLineHash').</summary>
public static class NoteLineHash
{
    /// <summary>The lowercase hex SHA-256 of the line's UTF-8 bytes, line ending excluded.</summary>
    /// <param name="line">The line.</param>
    /// <returns>64 hex characters.</returns>
    public static string Of(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(line)));
    }
}
