using System.Security.Cryptography;

namespace Aw.Context.Infrastructure.Docs;

/// <summary>The <c language="text">packet.json</c> beside a library file, when there is one: what a generated doc's source-sha256 is checked against.</summary>
/// <param name="FullPath">The absolute path.</param>
/// <param name="Size">The size in bytes.</param>
/// <param name="Modified">The last write time, to the millisecond.</param>
internal sealed record LibraryPacket(string FullPath, long Size, DateTimeOffset Modified)
{
    /// <summary>The file's sha256, lower-case hex; empty when it vanished or can't be read this run, so a later read changes the version.</summary>
    /// <returns>The hash, or an empty string.</returns>
    internal string Sha256()
    {
        try
        {
            using var stream = new FileStream(FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Convert.ToHexStringLower(SHA256.HashData(stream));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)   // boundary: a file that vanished or can't be read this run
        {
            return string.Empty;
        }
    }
}
