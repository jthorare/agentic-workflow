using System.Diagnostics;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.Launch;

/// <summary>The git adapter over a real repo with two commits.</summary>
[Lane(TestLane.Integration)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class GitScratchCheckoutsContractTests : ScratchCheckoutsContract, IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("aw-scratch-").FullName;

    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_root, recursive: true);
    }

    protected override IScratchCheckouts Create(out string repoPath, out (string First, string Second) commits)
    {
        repoPath = Path.Combine(_root, "repo");
        _ = Directory.CreateDirectory(repoPath);
        _ = Git(repoPath, "init", "-q");
        File.WriteAllText(Path.Combine(repoPath, "a.txt"), "one");
        _ = Git(repoPath, "add", "a.txt");
        _ = Git(repoPath, "-c", "user.name=t", "-c", "user.email=t@example.com", "commit", "-q", "-m", "one");
        var first = Git(repoPath, "rev-parse", "HEAD");
        File.WriteAllText(Path.Combine(repoPath, "a.txt"), "two");
        _ = Git(repoPath, "-c", "user.name=t", "-c", "user.email=t@example.com", "commit", "-q", "-am", "two");
        commits = (first, Git(repoPath, "rev-parse", "HEAD"));
        return new GitScratchCheckouts();
    }

    protected override string? HeadAt(string path) => Directory.Exists(path) ? Git(path, "rev-parse", "HEAD") : null;

    protected override string PathFor(string name) => Path.Combine(_root, "scratch", name);

    private static string Git(string directory, params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = directory };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return process.ExitCode == 0 ? output : throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed");
    }
}
