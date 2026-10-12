namespace Aw.Judge.Testing;

/// <summary>
/// What a program does when run, for <see cref="FakeProcessRunner"/> and for the shell scripts the real runner's tests
/// write. Standard output is, in order: the working directory's file names (one per line), the variable's value, the
/// standard input, <see cref="OutputSize"/> 'a's, then <see cref="StandardOutput"/>.
/// </summary>
public sealed record FakeProgram
{
    /// <summary>Printed last on standard output.</summary>
    public string StandardOutput { get; init; } = "";

    /// <summary>Printed last on standard error.</summary>
    public string StandardError { get; init; } = "";

    /// <summary>The exit code.</summary>
    public int ExitCode { get; init; }

    /// <summary>Lists the working directory's file names.</summary>
    public bool ListsWorkingDirectory { get; init; }

    /// <summary>Prints this environment variable's value.</summary>
    public string? PrintsVariable { get; init; }

    /// <summary>Copies its standard input to standard output.</summary>
    public bool EchoesStandardInput { get; init; }

    /// <summary>How many 'a's it writes to each of standard output and standard error first.</summary>
    public int OutputSize { get; init; }

    /// <summary>It is installed but the system refuses to start it (a file that isn't a program).</summary>
    public bool FailsToStart { get; init; }

    /// <summary>How long it runs before it writes anything else; past the request's timeout it is stopped.</summary>
    public TimeSpan Runs { get; init; }
}
