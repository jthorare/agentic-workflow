using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Aw.BugFix.Tests.Contracts;

namespace Aw.BugFix.Tests.Cli;

/// <summary>A temp git repository with a committed failing regression test, a ticket and a handoff, and a way to run bugfix-state.</summary>
internal sealed class CliWorkspace : IDisposable
{
    private readonly TempDirectory _root = new();

    public CliWorkspace()
    {
        Repo = _root.Repo("repo", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["src/app.txt"] = "drop cookie\n",
            ["tests/bug.test.sh"] = "echo checking\ngrep -q keep src/app.txt\n",
        });
        State = Path.Combine(_root.Path, "state", "eng-1");
        Ticket = Write("ticket.json", """{"source":"text","id":null,"title":"Login","brief":"Login shows a blank page","expected":"The dashboard opens","actual":"Blank page"}""");
        Handoff = Write("handoff.md", "status: diagnosed\n\n## Hypotheses\n\n| # | Hypothesis | Cause-site files | Likelihood | Result |\n|---|---|---|---|---|\n| 1 | Cookie dropped | `src/app.txt` | high | confirmed |\n\n## Root Cause\n\nThe cookie is dropped.\n");
    }

    public string Repo { get; }

    public string State { get; }

    public string Ticket { get; }

    public string Handoff { get; }

    public string Root => _root.Path;

    public string StateSha => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(State, "state.json"))));

    public string Write(string name, string text)
    {
        var path = Path.Combine(_root.Path, name);
        File.WriteAllText(path, text);
        return path;
    }

    /// <summary>A worktree on a new branch from HEAD with the fix committed.</summary>
    public string Fix(string branch)
    {
        var worktree = Path.Combine(_root.Path, branch);
        _ = TempDirectory.Git(Repo, "worktree", "add", "-q", "-b", branch, worktree);
        File.WriteAllText(Path.Combine(worktree, "src", "app.txt"), "keep cookie\n");
        _ = TempDirectory.Git(worktree, "commit", "-q", "-am", "fix");
        return worktree;
    }

    public (int Exit, string Out, string Err) Run(params string[] args) => RunWith(new Dictionary<string, string>(StringComparer.Ordinal), args);

    public (int Exit, string Out, string Err) RunWith(IReadOnlyDictionary<string, string> environment, params string[] args)
    {
        var start = new ProcessStartInfo(DotnetHost())
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Repo,
        };
        foreach (var arg in (string[])["exec", Path.Combine(AppContext.BaseDirectory, "bugfix-state.dll"), .. args])
        {
            start.ArgumentList.Add(arg);
        }

        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        start.Environment["AW_LOG_LEVEL"] = "none";
        foreach (var (name, value) in environment)
        {
            start.Environment[name] = value;
        }

        using var process = Process.Start(start)!; // why: Start returns null only when UseShellExecute reuses a process; it is off.
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output.Result, error);
    }

    public void Dispose() => _root.Dispose();

    private static string DotnetHost()
    {
        var root = Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", ".."));
        return Path.Combine(root, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
    }
}
