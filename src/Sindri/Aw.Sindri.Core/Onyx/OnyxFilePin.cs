namespace Aw.Sindri.Core.Onyx;

/// <summary>One pinned deployment file of the Onyx release (a row `file` of context/onyx.lock).</summary>
/// <param name="Path">The path under the release's deployment/ directory.</param>
/// <param name="Sha256">The file's sha256, 64 lowercase hex digits.</param>
public sealed record OnyxFilePin(string Path, string Sha256);
