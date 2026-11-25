using Aw.Meta.Core.Ports;
using Xunit;

namespace Aw.Meta.Testing;

/// <summary>The IWorktreeFiles contract: the same tests run against the in-memory fake and the real file-system adapter.</summary>
public abstract class WorktreeFilesContract
{
    private static readonly DateTimeOffset _old = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly string[] _skip = ["bin", ".git"];

    /// <summary>The adapter under test.</summary>
    /// <returns>The adapter.</returns>
    protected abstract IWorktreeFiles Create();

    /// <summary>A fresh directory holding the entries given (paths relative to it, a trailing slash for a directory; "" is the directory itself), each with the given modification time.</summary>
    /// <param name="entries">Relative path to time.</param>
    /// <returns>The directory.</returns>
    protected abstract string Arrange(IReadOnlyDictionary<string, DateTimeOffset> entries);

    /// <summary>A repo with the worktree root set the ways the owner can: in worktrees.json, in the environment, both or neither.</summary>
    /// <param name="fileRoot">The "root" the repo's .agentic-workflow/worktrees.json holds; null means no file.</param>
    /// <param name="environmentRoot">The AW_WORKTREE_ROOT value the adapter will see; null means unset.</param>
    /// <returns>The repo directory.</returns>
    protected abstract string ArrangeRoot(string? fileRoot, string? environmentRoot);

    /// <summary>Nothing set gives null, not an error.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task No_configured_root_is_null()
    {
        var repo = ArrangeRoot(fileRoot: null, environmentRoot: null);

        var result = await Create().ConfiguredRoot(repo, CancellationToken.None);

        Expect.That(result.IsSuccess && result.Value is null, "nothing set reads as null");
    }

    /// <summary>The file's root comes back as written.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_root_in_the_file_is_returned()
    {
        var repo = ArrangeRoot(fileRoot: "~/trees/app", environmentRoot: null);

        var result = await Create().ConfiguredRoot(repo, CancellationToken.None);

        Expect.That(result.IsSuccess && result.Value == "~/trees/app", "the file's root is returned");
    }

    /// <summary>The environment beats the file.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_environment_wins_over_the_file()
    {
        var repo = ArrangeRoot(fileRoot: "/from/file", environmentRoot: "/from/env");

        var result = await Create().ConfiguredRoot(repo, CancellationToken.None);

        Expect.That(result.IsSuccess && result.Value == "/from/env", "AW_WORKTREE_ROOT wins");
    }

    /// <summary>The newest time of the directory and its first two levels wins.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_newest_of_the_directory_and_its_top_two_levels_is_returned()
    {
        var newer = _old.AddDays(3);
        var directory = Arrange(new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal) { [string.Empty] = _old, ["src/"] = _old, ["src/a.cs"] = newer, ["README.md"] = _old.AddDays(1) });

        var result = await Create().NewestChange(directory, _skip, CancellationToken.None);

        Expect.That(result.IsSuccess, "it reads");
        Expect.That(result.Value == newer, "the level-two file's time wins");
    }

    /// <summary>Build output and git's own directory are left out, with what is under them.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Skipped_names_and_what_is_under_them_do_not_count()
    {
        var directory = Arrange(new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal) { [string.Empty] = _old, ["bin/"] = _old.AddDays(9), ["bin/x.dll"] = _old.AddDays(9), ["a.txt"] = _old.AddDays(1) });

        var result = await Create().NewestChange(directory, _skip, CancellationToken.None);

        Expect.That(result.IsSuccess && result.Value == _old.AddDays(1), "only the non-skipped entries count");
    }

    /// <summary>A directory that cannot be read is an error, so the caller keeps the worktree.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_directory_that_does_not_exist_is_an_error()
    {
        var result = await Create().NewestChange("/no/such/dir-for-aw-meta", _skip, CancellationToken.None);

        Expect.That(result.IsError, "a missing directory fails");
    }
}
