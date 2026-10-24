using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.BugFix;
using Aw.Sindri.Infrastructure.BugFix;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.BugFix;

/// <summary>
/// The real built bugfix-state (src/BugFix/Aw.BugFix.Host, built by the solution build) behind a <see cref="BugFixStateTool"/>, on a temp home: a git repo with a committed failing check,
/// a started bug fix whose check is frozen at the failing baseline, and candidates that are worktrees one commit past it.
/// </summary>
internal sealed class RealBugFixState : IDisposable
{
    private const string Check = "tests/bug.test.sh";

    private readonly TempHome _home = new();
    private readonly string _wrapper;
    private int _fixes;

    internal RealBugFixState()
    {
        Repo = Path.Combine(_home.Path, "repo");
        State = Path.Combine(_home.Path, "state", "eng-1");
        Empty = Path.Combine(_home.Path, "state", "none");
        Directory.CreateDirectory(Path.Combine(Repo, "src"));
        Directory.CreateDirectory(Path.Combine(Repo, "tests"));
        File.WriteAllText(Path.Combine(Repo, "src", "app.txt"), "drop cookie\n");
        File.WriteAllText(Path.Combine(Repo, Check), "grep -q keep src/app.txt\n");
        _ = Git(Repo, "init", "-q", "-b", "main");
        _ = Git(Repo, "add", "-A");
        _ = Git(Repo, "commit", "-q", "-m", "base");
        _wrapper = Path.Combine(_home.Path, "bugfix-state");
        _ = ExecutableScript.Write(_wrapper, $"GIT_CONFIG_GLOBAL=/dev/null\nAW_LOG_LEVEL=none\nAW_JUDGE_BIN='{NoJudge}'\nexport GIT_CONFIG_GLOBAL AW_LOG_LEVEL AW_JUDGE_BIN\ncd '{Repo}'\nexec dotnet '{BuiltDll()}' \"$@\"\n");
        var ticket = Path.Combine(_home.Path, "ticket.json");
        File.WriteAllText(ticket, """{"source":"text","id":null,"title":"Login","brief":"Login shows a blank page","expected":"The dashboard opens","actual":"Blank page"}""");
        var handoff = Path.Combine(_home.Path, "handoff.md");
        File.WriteAllText(handoff, "status: diagnosed\n\n## Hypotheses\n\n| # | Hypothesis | Cause-site files | Likelihood | Result |\n|---|---|---|---|---|\n| 1 | Cookie dropped | `src/app.txt` | high | confirmed |\n\n## Root Cause\n\nThe cookie is dropped.\n");
        _ = Run("init", "--state", State, "--ticket", ticket);
        _ = Run("advance", "investigate", "--state", State, "--evidence", handoff);
        var baseline = JsonDocument.Parse(Run("run-test", "--state", State, "--check", Check, "--", "bash", Check)).RootElement.GetProperty("evidence").GetString()!;
        _ = Run("advance", "reproduce", "--state", State, "--evidence", baseline, "--check", Check);
    }

    internal string State { get; }

    internal string Empty { get; }

    internal string Repo { get; }

    /// <summary>The judge binary bugfix-state is pointed at: a path with no file, so a judge on PATH is never asked.</summary>
    internal string NoJudge => Path.Combine(_home.Path, "no-judge");

    internal static FrozenCheck FrozenCheck { get; } = new("test", Check, ["bash", Check], ".");

    internal BugFixStateTool Tool() => new(new ProcessToolRunner(), name => string.Equals(name, "AW_BUGFIX_BIN", StringComparison.Ordinal) ? _wrapper : null);

    /// <summary>A worktree on a new branch from the baseline with one commit: the fix, or a change that leaves the check failing.</summary>
    internal CandidateSource Commit(bool fixes)
    {
        var number = (++_fixes).ToString(CultureInfo.InvariantCulture);
        var branch = "bugfix-" + number;
        var worktree = Path.Combine(_home.Path, branch);
        _ = Git(Repo, "worktree", "add", "-q", "-b", branch, worktree);
        File.WriteAllText(Path.Combine(worktree, "src", "app.txt"), fixes ? "keep cookie\n" : "drop cookie " + number + "\n");
        _ = Git(worktree, "commit", "-q", "-am", "change " + number);
        return new CandidateSource(branch, worktree);
    }

    public void Dispose() => _home.Dispose();

    private string Run(params string[] args)
    {
        var start = new ProcessStartInfo(_wrapper) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start)!;   // why: the program is the wrapper script, which starts
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"bugfix-state {string.Join(' ', args)} exited {process.ExitCode.ToString(CultureInfo.InvariantCulture)}: {output.Result}{error.Result}");
        return output.Result;
    }

    private static string Git(string directory, params string[] args)
    {
        var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in (string[])["-C", directory, "-c", "user.name=t", "-c", "user.email=t@example.invalid", "-c", "commit.gpgsign=false", .. args])
        {
            start.ArgumentList.Add(arg);
        }

        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        using var process = Process.Start(start)!;   // why: the program is git, which starts
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"git {string.Join(' ', args)} exited {process.ExitCode.ToString(CultureInfo.InvariantCulture)}: {error.Result}");
        return output.Result.Trim();
    }

    private static string BuiltDll()
    {
        var bin = Path.Combine(RepoRoot(), "artifacts", "bin", "Aw.BugFix.Host");
        var dlls = Directory.Exists(bin) ? Directory.GetFiles(bin, "bugfix-state.dll", SearchOption.AllDirectories) : [];
        Assert.True(dlls.Length > 0, "bugfix-state is not built: dotnet build src/BugFix/Aw.BugFix.Host");
        return dlls.OrderByDescending(File.GetLastWriteTimeUtc).First();
    }

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AgenticWorkflow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("AgenticWorkflow.slnx not found above the test assembly");
    }
}
