using Xunit;

namespace Aw.Evidence.Testing;

/// <summary>The IScratchDirectories contract: each directory is new, absolute and there until removed; removing it twice is fine.</summary>
public abstract class ScratchDirectoriesContract
{
    /// <summary>Arranges the port.</summary>
    /// <returns>The port and how to see a directory.</returns>
    protected abstract Task<ScratchUnderTest> ArrangeAsync();

    /// <summary>Two creations are two absolute directories that exist.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Each_directory_is_new_absolute_and_there()
    {
        var run = await ArrangeAsync();

        var first = run.Scratch.Create("aw-contract-");
        var second = run.Scratch.Create("aw-contract-");

        Expect.That(first.IsSuccess && second.IsSuccess && first.Value != second.Value, "two creations give two directories");
        Expect.That(first.Value.StartsWith('/') && run.Exists(first.Value) && run.Exists(second.Value), "they are absolute and exist");
        run.Scratch.Remove(first.Value);
        run.Scratch.Remove(second.Value);
    }

    /// <summary>Removing a directory removes it, and removing it again is fine.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Removing_a_directory_removes_it_and_a_second_removal_is_fine()
    {
        var run = await ArrangeAsync();
        var made = run.Scratch.Create("aw-contract-");

        run.Scratch.Remove(made.Value);
        run.Scratch.Remove(made.Value);

        Expect.That(!run.Exists(made.Value), "the directory is gone");
    }
}
