using Aw.Kernel;

namespace Aw.Sindri.Core;

public static partial class SindriErrors
{
    /// <summary>SND-RES-001: a disk holds more than its limit.</summary>
    public static readonly ErrorDefinition ResourceDiskOverSize = new("SND-RES-001", ErrorKind.Conflict, "A disk is over its size limit");

    /// <summary>SND-RES-002: two resources of one kind share a name.</summary>
    public static readonly ErrorDefinition ResourceNameClash = new("SND-RES-002", ErrorKind.Conflict, "Two resources of one kind share a name");

    /// <summary>SND-RES-003: a purge was refused because the owner is active or has a running run.</summary>
    public static readonly ErrorDefinition ResourcePurgeRefused = new("SND-RES-003", ErrorKind.Conflict, "The owner is active or has a running run, so its resources can't be purged");

    /// <summary>SND-RES-004: a path leaves the owner's folder.</summary>
    public static readonly ErrorDefinition ResourcePathEscapes = new("SND-RES-004", ErrorKind.Invalid, "A resource path leaves its owner's folder");

    /// <summary>SND-RES-005: a sandbox size is above the box's cap.</summary>
    public static readonly ErrorDefinition ResourceSandboxOverCap = new("SND-RES-005", ErrorKind.Invalid, "A sandbox size is above the box's cap");

    /// <summary>SND-RES-006: an owner, name or size is malformed.</summary>
    public static readonly ErrorDefinition ResourceInvalid = new("SND-RES-006", ErrorKind.Invalid, "A resource owner, name or size is malformed");

    /// <summary>SND-RES-007: a disk holds too many files and folders to measure.</summary>
    public static readonly ErrorDefinition ResourceDiskUnmeasured = new("SND-RES-007", ErrorKind.Conflict, "A disk holds too many files to measure");
}
