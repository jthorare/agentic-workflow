namespace Aw.Sindri.Toolkit;

/// <summary>The SDK's error codes. Sindri's own side of a toolkit run uses SND-TK-xxx; these are the toolkit process's.</summary>
public static class ToolkitErrors
{
    /// <summary>AW-TOOLKIT-001: the toolkit's declaration is wrong (no name, no JSON context, a tool that cannot be described).</summary>
    public static readonly ToolkitError InvalidDeclaration = new("AW-TOOLKIT-001", "The toolkit declaration is invalid");

    /// <summary>AW-TOOLKIT-002: a declared resource has no environment variable.</summary>
    public static readonly ToolkitError ResourceMissing = new("AW-TOOLKIT-002", "A declared resource is not provided");

    /// <summary>AW-TOOLKIT-003: a tool call named no tool, or its arguments do not match the tool's request type.</summary>
    public static readonly ToolkitError InvalidCall = new("AW-TOOLKIT-003", "The tool call is invalid");

    /// <summary>AW-TOOLKIT-004: a tool threw while running.</summary>
    public static readonly ToolkitError ToolFailed = new("AW-TOOLKIT-004", "The tool failed");

    /// <summary>AW-TOOLKIT-005: a database or disk resource is named but cannot be opened.</summary>
    public static readonly ToolkitError ResourceUnavailable = new("AW-TOOLKIT-005", "A resource cannot be opened");

    /// <summary>Every code, in code order.</summary>
    public static IReadOnlyList<ToolkitError> All { get; } =
        [InvalidDeclaration, ResourceMissing, InvalidCall, ToolFailed, ResourceUnavailable];
}
