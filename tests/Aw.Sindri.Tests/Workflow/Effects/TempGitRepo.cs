using System.Diagnostics;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Workflow.Effects;

/// <summary>A throwaway git repo with one empty commit on 'main', with no global or system git config.</summary>
public sealed class TempGitRepo : IDisposable
{
    private readonly TempHome _temp = new();

    private TempGitRepo()
    {
        Path = System.IO.Path.Combine(_temp.Path, "repo");
        _ = Directory.CreateDirectory(Path);
    }

    /// <summary>The checkout.</summary>
    public string Path { get; }

    /// <summary>A repo with one commit (<paramref name="commits"/> 0 leaves it empty).</summary>
    public static async Task<TempGitRepo> Create(int commits = 1)
    {
        var repo = new TempGitRepo();
        _ = await repo.Git("init", "-q", "-b", "main");
        for (var i = 0; i < commits; i++)
        {
            _ = await repo.Git("-c", "user.email=test@example.invalid", "-c", "user.name=test", "commit", "-q", "--allow-empty", "-m", "init");
        }

        return repo;
    }

    public void Dispose() => _temp.Dispose();

    /// <summary>Runs git in the repo; asserts it exits 0 and returns its trimmed output.</summary>
    public async Task<string> Git(params string[] args)
    {
        var (exit, output, error) = await Run(args);
        Assert.True(exit == 0, $"git {string.Join(' ', args)}: {error}");
        return output;
    }

    /// <summary>Runs git in the repo and returns its exit code, output and error.</summary>
    public async Task<(int Exit, string Output, string Error)> Run(params string[] args)
    {
        var start = new ProcessStartInfo("git", ["-C", Path, .. args]) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        using var git = Process.Start(start)!;   // why: Start returns a process for a command it found
        var output = git.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = git.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await git.WaitForExitAsync(TestContext.Current.CancellationToken);
        return (git.ExitCode, (await output).Trim(), await error);
    }
}
