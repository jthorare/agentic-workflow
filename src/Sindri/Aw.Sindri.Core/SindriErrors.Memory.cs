using Aw.Kernel;

namespace Aw.Sindri.Core;

public static partial class SindriErrors
{
    /// <summary>SND-MEM-001: no memory has that name.</summary>
    public static readonly ErrorDefinition MemoryNotFound = new("SND-MEM-001", ErrorKind.NotFound, "Sindri has no memory with that name");

    /// <summary>SND-MEM-002: a memory with that name exists.</summary>
    public static readonly ErrorDefinition MemoryNameTaken = new("SND-MEM-002", ErrorKind.Conflict, "A memory with that name already exists");

    /// <summary>SND-MEM-003: the fact text is empty or longer than 2,000 characters.</summary>
    public static readonly ErrorDefinition MemoryFactInvalid = new("SND-MEM-003", ErrorKind.Invalid, "A fact is 1 to 2,000 characters of text");

    /// <summary>SND-MEM-004: the embedding model did not answer, so recall used keywords (informational).</summary>
    public static readonly ErrorDefinition MemoryEmbedUnavailable = new("SND-MEM-004", ErrorKind.Unavailable, "The embedding model is not available, so recall used keywords");

    /// <summary>SND-MEM-005: the match level is not loose, balanced or strict.</summary>
    public static readonly ErrorDefinition MemoryMatchLevelInvalid = new("SND-MEM-005", ErrorKind.Invalid, "A match level is loose, balanced or strict");

    /// <summary>SND-MEM-006: the memory name is not lowercase letters, digits and '-'.</summary>
    public static readonly ErrorDefinition MemoryNameInvalid = new("SND-MEM-006", ErrorKind.Invalid, "A memory name is lowercase letters, digits and '-', 1 to 48 characters");
}
