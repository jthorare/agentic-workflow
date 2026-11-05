namespace Aw.Sindri.Features.ModelCheck;

/// <summary>What a spec folder entry is on disk: only regular files may sit in a spec folder (MDL-SPEC-001).</summary>
internal enum SpecFileKind
{
    /// <summary>A regular file.</summary>
    Regular,

    /// <summary>A symbolic link.</summary>
    Symlink,

    /// <summary>Anything else: a device, a socket, a directory other than gaps/.</summary>
    Other,
}
