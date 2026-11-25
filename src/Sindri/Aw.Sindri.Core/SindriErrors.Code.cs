using Aw.Kernel;

namespace Aw.Sindri.Core;

public static partial class SindriErrors
{
    /// <summary>SND-CODE-001: a Code node's C# does not compile; the message lists each error with its line and column.</summary>
    public static readonly ErrorDefinition CodeCompileFailed = new("SND-CODE-001", ErrorKind.Invalid, "The code does not compile");

    /// <summary>SND-CODE-002: the code ran past its timeout and the runner was killed.</summary>
    public static readonly ErrorDefinition CodeTimeout = new("SND-CODE-002", ErrorKind.Timeout, "The code ran past its timeout");

    /// <summary>SND-CODE-003: the code used more memory than its limit allows.</summary>
    public static readonly ErrorDefinition CodeMemory = new("SND-CODE-003", ErrorKind.BudgetExceeded, "The code used more memory than its limit");

    /// <summary>SND-CODE-004: the code threw, or its result is not JSON.</summary>
    public static readonly ErrorDefinition CodeFailed = new("SND-CODE-004", ErrorKind.Invalid, "The code failed while running");

    /// <summary>SND-CODE-005: the runner could not be started (not installed, or the sandbox refused it).</summary>
    public static readonly ErrorDefinition CodeRunnerUnavailable = new("SND-CODE-005", ErrorKind.Unavailable, "The code runner could not be started");
}
