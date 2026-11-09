namespace Aw.Sindri.Core.Profile;

/// <summary>
/// The memory of the live profile reads keep between two edits (#2258 PR 1). Sindri's own profile writers call <see cref="Invalidate"/> right after the atomic rename, so the next read
/// parses the file even when its last write time and length did not change (two writes in one clock tick).
/// </summary>
public interface IProfileCache
{
    /// <summary>Drops what was kept; the next read parses the file.</summary>
    public void Invalidate();
}
