using System.Diagnostics;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.CheckTraits;

/// <summary>
/// A throwaway git repo for trait-check: a 'no-todo' block trait over 'src/**', 'src/app.ts' and 'docs/readme.md'
/// committed, 'origin/main' at that commit, then 'src/app.ts' edited (a TODO and a GitHub token) and not committed.
/// </summary>
public sealed class TraitRepo : IDisposable
{
    public static readonly string TraitText = string.Join(
        '\n',
        "version: 1",
        "name: no-todo",
        "subject: TODO comments",
        "requirement: No TODO comments are left in source files.",
        "purpose: Work left undone is tracked in issues.",
        "guidance: Open an issue and link it.",
        "paths:",
        "  - \"src/**\"",
        "severity: block");

    public TraitRepo()
    {
        Root = Directory.CreateTempSubdirectory("aw-trait-repo-").FullName;
        _ = Git("init", "-q", "-b", "main");
        Write(".agents/traits/no-todo.yaml", TraitText);
        Write("src/app.ts", "export const a = 1;\n");
        Write("docs/readme.md", "# readme\n");
        _ = Git("add", "-A");
        _ = Git("-c", "user.name=t", "-c", "user.email=t@example.com", "-c", "commit.gpgsign=false", "commit", "-q", "-m", "init");
        _ = Git("update-ref", "refs/remotes/origin/main", "HEAD");
        Write("src/app.ts", "export const a = 1;\n// TODO: remove\nexport const token = '" + SampleSecrets.GitHubToken + "';\n");
    }

    public string Root { get; }

    public void Write(string relative, string text)
    {
        var path = Path.Combine(Root, relative);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    public string Git(params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = Root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        // Nothing of the developer's git configuration reaches the repo.
        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        start.Environment["GIT_CONFIG_SYSTEM"] = "/dev/null";
        using var process = Process.Start(start)!;   // why: git is a prerequisite of the whole build
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0 ? output : throw new InvalidOperationException($"git {string.Join(' ', arguments)}: {process.StandardError.ReadToEnd()}");
    }

    public void Dispose() => Directory.Delete(Root, recursive: true);
}
