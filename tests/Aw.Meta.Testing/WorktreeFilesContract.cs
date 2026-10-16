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
