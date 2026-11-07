using Aw.Context.Core.Library;

namespace Aw.Context.Infrastructure.Docs;

/// <summary>One document the pull hands over.</summary>
/// <param name="Path">The document's path.</param>
/// <param name="Blob">Its blob id.</param>
/// <param name="Head">The commit the document was read at: the packet is read as a checkout of it writes it.</param>
/// <param name="PacketPath">The path of the packet.json beside the document.</param>
/// <param name="PacketBlob">The blob id of the regular-file packet.json in its folder, or null when there is none (a symlink is none).</param>
/// <param name="Version">The version the record carries: the blob id, or <see cref="GeneratedSource.Version"/> when a packet sits beside it.</param>
internal sealed record RepoDocChange(string Path, string Blob, string Head, string PacketPath, string? PacketBlob, string Version);
