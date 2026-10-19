namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>What a host path is, as far as the argv builder cares (a denied path is hidden differently by kind).</summary>
internal enum HostPathKind
{
    /// <summary>Nothing there.</summary>
    Missing,

    /// <summary>A directory.</summary>
    Directory,

    /// <summary>A file, socket or anything else that is not a directory.</summary>
    File,
}
