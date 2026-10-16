using Xunit;

namespace Aw.Evidence.Testing;

/// <summary>
/// The IExecutableLocator contract, over one layout under a root: bin/tool (executable), bin/notes.txt (a plain file),
/// link-to-tool (a symlink to bin/tool), link-to-bin (a symlink to bin/) and outside/other (executable).
/// </summary>
public abstract class ExecutableLocatorContract
{
    /// <summary>Arranges the layout and returns a locator over it.</summary>
    /// <returns>The locator and the root.</returns>
    protected abstract Task<LocatorUnderTest> ArrangeAsync();

    /// <summary>An executable file resolves to itself.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_executable_file_is_its_own_real_path()
    {
        var layout = await ArrangeAsync();

        Expect.That(layout.Locator.RealExecutable(layout.Root + "/bin/tool") == layout.Root + "/bin/tool", "bin/tool resolves to itself");
    }

    /// <summary>A symlink resolves to where it points.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_symlink_to_an_executable_resolves_to_its_target()
    {
        var layout = await ArrangeAsync();

        Expect.That(layout.Locator.RealExecutable(layout.Root + "/link-to-tool") == layout.Root + "/bin/tool", "the link resolves to bin/tool");
    }

    /// <summary>A path through a symlinked directory resolves to the real file.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_path_through_a_symlinked_directory_resolves_to_the_real_file()
    {
        var layout = await ArrangeAsync();

        Expect.That(layout.Locator.RealExecutable(layout.Root + "/link-to-bin/tool") == layout.Root + "/bin/tool", "link-to-bin/tool resolves to bin/tool");
    }

    /// <summary>A plain file, a directory and a missing path are not executables.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_plain_file_a_directory_and_a_missing_path_are_not_executables()
    {
        var layout = await ArrangeAsync();

        Expect.That(layout.Locator.RealExecutable(layout.Root + "/bin/notes.txt") is null, "a non-executable file is null");
        Expect.That(layout.Locator.RealExecutable(layout.Root + "/bin") is null, "a directory is null");
        Expect.That(layout.Locator.RealExecutable(layout.Root + "/bin/absent") is null, "a missing path is null");
    }

    /// <summary>A directory resolves, through a symlink too; a file or a missing path is not a directory.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_directory_resolves_through_a_symlink_and_a_file_is_not_a_directory()
    {
        var layout = await ArrangeAsync();

        Expect.That(layout.Locator.RealDirectory(layout.Root + "/bin") == layout.Root + "/bin", "bin is its own real path");
        Expect.That(layout.Locator.RealDirectory(layout.Root + "/link-to-bin") == layout.Root + "/bin", "link-to-bin resolves to bin");
        Expect.That(layout.Locator.RealDirectory(layout.Root + "/bin/tool") is null, "a file is not a directory");
        Expect.That(layout.Locator.RealDirectory(layout.Root + "/absent") is null, "a missing path is not a directory");
    }

    /// <summary>A regular file, executable or not, is found through its links; a directory and a missing path are not files.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_regular_file_resolves_through_its_links_and_a_directory_is_not_a_file()
    {
        var layout = await ArrangeAsync();

        Expect.That(layout.Locator.RealFile(layout.Root + "/bin/notes.txt") == layout.Root + "/bin/notes.txt", "a plain file is its own real path");
        Expect.That(layout.Locator.RealFile(layout.Root + "/link-to-tool") == layout.Root + "/bin/tool", "a link to a file resolves to the file");
        Expect.That(layout.Locator.RealFile(layout.Root + "/bin") is null, "a directory is not a file");
        Expect.That(layout.Locator.RealFile(layout.Root + "/nothing") is null, "a missing path is not a file");
    }
}
