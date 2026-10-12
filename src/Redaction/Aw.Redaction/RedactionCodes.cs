namespace Aw.Redaction;

/// <summary>Error codes Redaction reports (AW-REDACT-NNN). Redaction is BCL-only, so these are plain codes in the
/// Kernel catalogue's format rather than ErrorDefinitions.</summary>
public static class RedactionCodes
{
    /// <summary>A pattern ran past its match timeout, so the text was withheld instead of passed on.</summary>
    public const string MatchTimeout = "AW-REDACT-001";

    /// <summary>A configured extra pattern's id is malformed, reserved (a built-in rule id, blocked, secret-field) or
    /// repeated.</summary>
    public const string InvalidPatternId = "AW-REDACT-002";

    /// <summary>A configured extra pattern is empty, too long, not a valid regex, can match an empty text (or times
    /// out trying to), or uses a Unicode-aware shorthand (\d, \w, \s, \b, or a Unicode category \p{…}) instead of an explicit ASCII class.</summary>
    public const string InvalidPattern = "AW-REDACT-003";
}
