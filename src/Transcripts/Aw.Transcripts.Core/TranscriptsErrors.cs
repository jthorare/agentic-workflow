using Aw.Kernel;

namespace Aw.Transcripts.Core;

/// <summary>The Transcripts error catalogue (codes AW-TRANSCRIPTS-NNN). No message carries transcript text.</summary>
public static class TranscriptsErrors
{
    /// <summary>AW-TRANSCRIPTS-001.</summary>
    public static readonly ErrorDefinition Invalid = new("AW-TRANSCRIPTS-001", ErrorKind.Invalid, "Transcripts input failed validation");

    /// <summary>AW-TRANSCRIPTS-002: no transcript file under the roots has this ref (it moved, was deleted, or never existed).</summary>
    public static readonly ErrorDefinition SessionNotFound = new("AW-TRANSCRIPTS-002", ErrorKind.NotFound, "No transcript under the roots has this ref");

    /// <summary>AW-TRANSCRIPTS-003: a transcript file exists but could not be opened or read.</summary>
    public static readonly ErrorDefinition Unreadable = new("AW-TRANSCRIPTS-003", ErrorKind.Unavailable, "A transcript file could not be read");

    /// <summary>AW-TRANSCRIPTS-004: two transcript files hash to the same ref, so the ref can't name one of them.</summary>
    public static readonly ErrorDefinition AmbiguousRef = new("AW-TRANSCRIPTS-004", ErrorKind.Conflict, "Two transcript files share this ref");

    /// <summary>Every Transcripts error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } = [Invalid, SessionNotFound, Unreadable, AmbiguousRef];
}
