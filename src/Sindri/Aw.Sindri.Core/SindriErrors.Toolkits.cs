using Aw.Kernel;

namespace Aw.Sindri.Core;

public static partial class SindriErrors
{
    /// <summary>SND-TK-001: the toolkit is unbuilt, or its source changed since the last good build.</summary>
    public static readonly ErrorDefinition ToolkitNotBuilt = new("SND-TK-001", ErrorKind.Conflict, "The toolkit isn't built for its current source (fix: sindri toolkit build <name>)");

    /// <summary>SND-TK-002: the build or the toolkit's tests failed.</summary>
    public static readonly ErrorDefinition ToolkitTestsFailed = new("SND-TK-002", ErrorKind.Conflict, "The toolkit's build or tests failed");

    /// <summary>SND-TK-003: toolkit.json isn't the SDK's manifest shape.</summary>
    public static readonly ErrorDefinition ToolkitManifestInvalid = new("SND-TK-003", ErrorKind.Invalid, "The toolkit's manifest isn't valid");

    /// <summary>SND-TK-004: the toolkit process ended.</summary>
    public static readonly ErrorDefinition ToolkitProcessDied = new("SND-TK-004", ErrorKind.Unavailable, "The toolkit's process ended");

    /// <summary>SND-TK-005: the toolkit is paused.</summary>
    public static readonly ErrorDefinition ToolkitPaused = new("SND-TK-005", ErrorKind.Conflict, "The toolkit is paused (fix: toolkit_stop, then toolkit_start)");

    /// <summary>SND-TK-006: a tool call ran past its timeout.</summary>
    public static readonly ErrorDefinition ToolkitCallTimedOut = new("SND-TK-006", ErrorKind.Unavailable, "The toolkit tool didn't answer in time");

    /// <summary>SND-TK-007: the toolkit has no such tool.</summary>
    public static readonly ErrorDefinition ToolkitToolUnknown = new("SND-TK-007", ErrorKind.NotFound, "No such toolkit tool");

    /// <summary>SND-TK-008: this box can't sandbox a plain process.</summary>
    public static readonly ErrorDefinition ToolkitSandboxUnavailable = new("SND-TK-008", ErrorKind.Unavailable, "A toolkit can't start: this box has no process sandbox");

    /// <summary>SND-TK-009: no toolkit has that name.</summary>
    public static readonly ErrorDefinition ToolkitNotFound = new("SND-TK-009", ErrorKind.NotFound, "No such toolkit");

    /// <summary>SND-TK-010: a toolkit or tool name is malformed or too long for a client.</summary>
    public static readonly ErrorDefinition ToolkitNameInvalid = new("SND-TK-010", ErrorKind.Invalid, "A toolkit or tool name isn't valid");

    /// <summary>SND-TK-011: the toolkit's tool ran and answered with an error.</summary>
    public static readonly ErrorDefinition ToolkitToolFailed = new("SND-TK-011", ErrorKind.Unavailable, "The toolkit tool returned an error");
}
