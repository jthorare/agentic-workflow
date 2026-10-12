using System.Diagnostics;
using System.Globalization;

namespace Aw.Context.Testing.Fixtures;

/// <summary>
/// Runs git in the fixture repo with no user, system or hook configuration and none of the caller's GIT_* variables
/// (a test run inside a git hook would otherwise write to the outer repo), so the history is the same on every machine.
/// </summary>
internal sealed class GitRunner(string repo, string emptyConfig, FixtureAuthor author)
{
    public string Run(DateTimeOffset at, params string[] args)
    {
        var start = new ProcessStartInfo("git")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = repo,
        };
        Configure(start, at);
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        using var git = Process.Start(start) ?? throw new InvalidOperationException("git did not start.");
        var stderr = git.StandardError.ReadToEndAsync();
        var stdout = git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        return git.ExitCode == 0
            ? stdout.TrimEnd('\n')
            : throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"git {string.Join(' ', args)} exited {git.ExitCode}: {stderr.Result.Trim()}"));
    }

    private void Configure(ProcessStartInfo start, DateTimeOffset at)
    {
        foreach (var name in start.Environment.Keys.Where(k => k.StartsWith("GIT_", StringComparison.Ordinal)).ToList())
        {
            _ = start.Environment.Remove(name);
        }

        var date = at.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        start.Environment["GIT_CONFIG_GLOBAL"] = emptyConfig;
        start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        start.Environment["GIT_AUTHOR_NAME"] = author.Name;
        start.Environment["GIT_AUTHOR_EMAIL"] = author.Email;
        start.Environment["GIT_COMMITTER_NAME"] = author.Name;
        start.Environment["GIT_COMMITTER_EMAIL"] = author.Email;
        start.Environment["GIT_AUTHOR_DATE"] = date;
        start.Environment["GIT_COMMITTER_DATE"] = date;
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
    }
}
