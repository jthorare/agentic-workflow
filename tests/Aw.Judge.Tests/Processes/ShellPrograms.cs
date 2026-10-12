using System.Globalization;
using System.Text;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.Processes;

/// <summary>Writes <see cref="FakeProgram"/>s as executable sh scripts in a temporary directory (deleted on dispose), to put on a PATH.</summary>
internal sealed class ShellPrograms : IDisposable
{
    public string Directory { get; } = System.IO.Directory.CreateTempSubdirectory("judge-bin-").FullName;

    /// <summary>A PATH value of just this directory.</summary>
    public string Path => Directory;

    /// <summary>Reads PATH as this directory, nothing else.</summary>
    public Func<string, string?> Environment => name => name == "PATH" ? Directory : null;

    public static string Quote(string text) => "'" + text.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    public string Install(string name, FakeProgram program) =>
        program.FailsToStart ? InstallFile(name, "not a program\n") : InstallScript(name, Script(program));

    public string InstallScript(string name, string body) => InstallFile(name, "#!/bin/sh\n" + body);

    // An executable file; without a '#!' line the system refuses to run it (ENOEXEC).
    private string InstallFile(string name, string content)
    {
        var file = System.IO.Path.Combine(Directory, name);
        File.WriteAllText(file, content);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        return file;
    }

    public void Dispose() => System.IO.Directory.Delete(Directory, recursive: true);

    private static string Script(FakeProgram program)
    {
        var script = new StringBuilder();
        if (program.Runs > TimeSpan.Zero)
        {
            script.Append(CultureInfo.InvariantCulture, $"sleep {Math.Ceiling(program.Runs.TotalSeconds)}\n");
        }

        if (program.ListsWorkingDirectory)
        {
            script.Append("ls\n");
        }

        if (program.PrintsVariable is { } variable)
        {
            script.Append(CultureInfo.InvariantCulture, $"printf '%s' \"${variable}\"\n");
        }

        if (program.EchoesStandardInput)
        {
            script.Append("cat\n");
        }

        if (program.OutputSize > 0)
        {
            script.Append(CultureInfo.InvariantCulture, $"head -c {program.OutputSize} /dev/zero | tr '\\0' a\n");
            script.Append(CultureInfo.InvariantCulture, $"head -c {program.OutputSize} /dev/zero | tr '\\0' a >&2\n");
        }

        script.Append(CultureInfo.InvariantCulture, $"printf '%s' {Quote(program.StandardOutput)}\n");
        script.Append(CultureInfo.InvariantCulture, $"printf '%s' {Quote(program.StandardError)} >&2\n");
        script.Append(CultureInfo.InvariantCulture, $"exit {program.ExitCode}\n");
        return script.ToString();
    }
}
