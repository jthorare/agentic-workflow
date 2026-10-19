using Aw.Evidence.Infrastructure.Processes;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>The real locator over a temp directory with real files and symlinks.</summary>
[Lane(TestLane.Integration)]
public sealed class FileExecutableLocatorContractTests : ExecutableLocatorContract, IDisposable
{
    private readonly TempDirectory _root = new();

    protected override async Task<LocatorUnderTest> ArrangeAsync()
    {
        var root = CanonicalPath.Of(_root.Path);   // the locator answers real paths; on macOS the temp path is under a link
        _ = Directory.CreateDirectory(Path.Combine(root, "bin"));
        _ = Directory.CreateDirectory(Path.Combine(root, "outside"));
        _ = StubCommand.Write(_root, "bin/tool", "exit 0");
        _ = StubCommand.Write(_root, "outside/other", "exit 0");
        await File.WriteAllTextAsync(Path.Combine(root, "bin", "notes.txt"), "notes", TestContext.Current.CancellationToken);
        _ = File.CreateSymbolicLink(Path.Combine(root, "link-to-tool"), Path.Combine(root, "bin", "tool"));
        _ = File.CreateSymbolicLink(Path.Combine(root, "link-to-bin"), Path.Combine(root, "bin"));
        return new LocatorUnderTest(new FileExecutableLocator(), root);
    }

    public void Dispose() => _root.Dispose();
}
