using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

/// <summary>The in-memory worktree files pass the same contract as the real adapter.</summary>
[Lane(TestLane.Unit)]
public sealed class InMemoryWorktreeFilesContractTests : WorktreeFilesContract
{
    private readonly InMemoryWorktreeFiles _fake = new();
    private int _directories;

    protected override IWorktreeFiles Create() => _fake;

    protected override string Arrange(IReadOnlyDictionary<string, DateTimeOffset> entries)
    {
        var directory = "/fake/tree-" + (++_directories).ToString(System.Globalization.CultureInfo.InvariantCulture);
        _fake.Arrange(directory, entries.ToDictionary(e => e.Key.TrimEnd('/'), e => e.Value, StringComparer.Ordinal));
        return directory;
    }
}
