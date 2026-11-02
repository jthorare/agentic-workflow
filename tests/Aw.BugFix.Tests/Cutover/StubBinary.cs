using Aw.BugFix.Tests.Contracts;
using Aw.Kernel.Testing;

namespace Aw.BugFix.Tests.Cutover;

/// <summary>A stand-in for the judge or ui-evidence binary: a bash script in its own temp directory that logs how it was called and answers from files the test writes.</summary>
internal sealed class StubBinary : IDisposable
{
    private readonly TempDirectory _dir = new();

    public StubBinary(string name, string script)
    {
        Directory = _dir.Path;
        Path = System.IO.Path.Combine(Directory, name);
        // Written and warmed up by ExecutableScript: a fresh executable can fail to start with ETXTBSY while a parallel test forks (#1250).
        var text = script.Replace("@DIR@", Directory, StringComparison.Ordinal);
        var newline = text.IndexOf('\n', StringComparison.Ordinal);
        var (interpreter, body) = text.StartsWith("#!", StringComparison.Ordinal) && newline > 0 ? (text[2..newline], text[(newline + 1)..]) : ("/bin/sh", text);
        _ = ExecutableScript.Write(Path, body, interpreter);
    }

    public string Directory { get; }

    public string Path { get; }

    /// <summary>Writes a file the stub reads (its exit code, its canned output).</summary>
    public void Set(string file, string text) => File.WriteAllText(System.IO.Path.Combine(Directory, file), text);

    /// <summary>Reads a file the stub wrote (its argv, its stdin).</summary>
    public string Read(string file) => File.ReadAllText(System.IO.Path.Combine(Directory, file));

    public void Dispose() => _dir.Dispose();

    /// <summary>The environment an adapter is given: the named variables only.</summary>
    public static Func<string, string?> Environment(params (string Name, string Value)[] variables) =>
        name => variables.Where(v => v.Name == name).Select(static v => v.Value).FirstOrDefault();
}
