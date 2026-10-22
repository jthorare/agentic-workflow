using Aw.Kernel;

namespace Aw.Packets.Core;

/// <summary>The Packets error catalogue (codes AW-PACKETS-NNN).</summary>
public static class PacketsErrors
{
    /// <summary>AW-PACKETS-001.</summary>
    public static readonly ErrorDefinition Invalid = new("AW-PACKETS-001", ErrorKind.Invalid, "Packets input failed validation");

    /// <summary>AW-PACKETS-002: no packet directory, or no packet.json in it.</summary>
    public static readonly ErrorDefinition NotFound = new("AW-PACKETS-002", ErrorKind.NotFound, "Packet not found");

    /// <summary>AW-PACKETS-003: a packet file is malformed or missing, or a link leaves the packet directory.</summary>
    public static readonly ErrorDefinition Malformed = new("AW-PACKETS-003", ErrorKind.Invalid, "Packet is malformed");

    /// <summary>AW-PACKETS-004: a packet file declares a schemaVersion this build doesn't read.</summary>
    public static readonly ErrorDefinition UnsupportedSchema = new("AW-PACKETS-004", ErrorKind.Invalid, "Unsupported packet schema version");

    /// <summary>AW-PACKETS-005: the base commit isn't in the repository.</summary>
    public static readonly ErrorDefinition CommitNotFound = new("AW-PACKETS-005", ErrorKind.NotFound, "Base commit not found");

    /// <summary>AW-PACKETS-006: the repository can't be read (not a repository, git missing, a timeout).</summary>
    public static readonly ErrorDefinition RepoUnavailable = new("AW-PACKETS-006", ErrorKind.Unavailable, "Repository unreadable");

    /// <summary>AW-PACKETS-007: the freeze record can't be written under packets/.</summary>
    public static readonly ErrorDefinition FreezeLogUnavailable = new("AW-PACKETS-007", ErrorKind.Unavailable, "Freeze log unwritable");

    /// <summary>AW-PACKETS-008: a packet file exists but can't be read.</summary>
    public static readonly ErrorDefinition Unreadable = new("AW-PACKETS-008", ErrorKind.Unavailable, "Packet unreadable");

    /// <summary>AW-PACKETS-009: the sizes file 'validate --sizes' names is missing, unreadable, malformed or not schemaVersion 1.</summary>
    public static readonly ErrorDefinition SizesUnusable = new("AW-PACKETS-009", ErrorKind.Unavailable, "Sizes file unusable");

    /// <summary>AW-PACKETS-010: the seal file 'validate --seal' names is missing, unreadable, malformed or not schemaVersion 1.</summary>
    public static readonly ErrorDefinition SealUnusable = new("AW-PACKETS-010", ErrorKind.Unavailable, "Seal file unusable");

    /// <summary>Every Packets error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } =
        [Invalid, NotFound, Malformed, UnsupportedSchema, CommitNotFound, RepoUnavailable, FreezeLogUnavailable, Unreadable, SizesUnusable, SealUnusable];
}
