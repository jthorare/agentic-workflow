using Aw.Kernel;

namespace Aw.Transcripts.Core;

/// <summary>The Transcripts error catalogue (codes AW-TRANSCRIPTS-NNN).</summary>
public static class TranscriptsErrors
{
    /// <summary>AW-TRANSCRIPTS-001.</summary>
    public static readonly ErrorDefinition Invalid = new("AW-TRANSCRIPTS-001", ErrorKind.Invalid, "Transcripts input failed validation");

    /// <summary>Every Transcripts error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } = [Invalid];
}
