using Aw.Sindri.Core.Prove;
using Xunit;

namespace Aw.Sindri.Testing.Prove;

/// <summary>The contract every <see cref="IArtifactFiles"/> honours (BS-13): the hash of the bytes, null for a missing file, an error for a path outside the worktree.</summary>
public abstract class ArtifactFilesContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The adapter under test.</summary>
    /// <returns>The files.</returns>
    protected abstract IArtifactFiles Create();

    /// <summary>The phase worktree the files are put in.</summary>
    protected abstract string Workspace { get; }

    /// <summary>Puts a file in the worktree.</summary>
    /// <param name="path">The path, relative to the worktree.</param>
    /// <param name="text">The content.</param>
    protected abstract void Put(string path, string text);

    /// <summary>A relative path is hashed from its bytes, with the 'sha256:' prefix.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_relative_file_is_hashed_from_its_bytes()
    {
        Put("evidence/shot.txt", "hello");

        var read = await Create().Sha256(Workspace, "evidence/shot.txt", Token);

        Expect.True(read.IsSuccess);
        Expect.Equal(InMemoryArtifactFiles.HashOf("hello"), read.Value);
    }

    /// <summary>An absolute path inside the worktree is read the same as the relative one.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_absolute_path_inside_the_worktree_reads_the_same_file()
    {
        Put("a.txt", "hello");

        var read = await Create().Sha256(Workspace, Path.Combine(Workspace, "a.txt"), Token);

        Expect.Equal(InMemoryArtifactFiles.HashOf("hello"), read.Value);
    }

    /// <summary>Different bytes make a different hash.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Different_content_has_a_different_hash()
    {
        Put("a.txt", "one");
        Put("b.txt", "two");
        var files = Create();

        var a = await files.Sha256(Workspace, "a.txt", Token);
        var b = await files.Sha256(Workspace, "b.txt", Token);

        Expect.True(a.Value != b.Value);
    }

    /// <summary>A file that isn't there is a success with no hash, so the caller can name the item it fails to back.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_missing_file_is_reported_as_missing_and_not_as_an_error()
    {
        var read = await Create().Sha256(Workspace, "evidence/none.txt", Token);

        Expect.True(read.IsSuccess);
        Expect.True(read.Value is null);
    }

    /// <summary>A path that leaves the worktree is refused, whether by '..' or by an absolute path.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The test.</returns>
    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("evidence/../../outside.txt")]
    [InlineData("/etc/hostname")]
    public async Task A_path_outside_the_worktree_is_refused(string path)
    {
        var read = await Create().Sha256(Workspace, path, Token);

        Expect.True(read.IsError);
    }
}
