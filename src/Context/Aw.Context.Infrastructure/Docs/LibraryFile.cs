namespace Aw.Context.Infrastructure.Docs;

/// <summary>One markdown file of a library root.</summary>
/// <param name="Relative">The root-relative path, '/'-separated.</param>
/// <param name="FullPath">The absolute path.</param>
/// <param name="Size">The size in bytes.</param>
/// <param name="Modified">The last write time, to the millisecond.</param>
/// <param name="Packet">The packet.json in the same folder, or null.</param>
internal sealed record LibraryFile(string Relative, string FullPath, long Size, DateTimeOffset Modified, LibraryPacket? Packet = null);
