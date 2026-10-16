using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Integration)]
public sealed class DirectoryWorktreeFilesContractTests : WorktreeFilesContract, IDisposable
{
    private readonly List<string> _directories = [];

    protected override IWorktreeFiles Create() => new DirectoryWorktreeFiles();

    protected override string Arrange(IReadOnlyDictionary<string, DateTimeOffset> entries)
    {
        var root = Directory.CreateTempSubdirectory("aw-meta-files-").FullName;
        _directories.Add(root);
        foreach (var path in entries.Keys.Where(k => k.Length > 0))
        {
            var full = Path.Combine(root, path.TrimEnd('/'));
            if (path.EndsWith('/'))
            {
                _ = Directory.CreateDirectory(full);
            }
            else
            {
                _ = Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                File.WriteAllText(full, "x");
            }
        }

        // Stamp the deepest entries first: writing into a directory moves its own time.
        foreach (var (path, time) in entries.OrderByDescending(e => e.Key.Count(c => c == '/')).ThenByDescending(e => e.Key.Length))
        {
            var full = Path.Combine(root, path.TrimEnd('/'));
            if (Directory.Exists(full))
            {
                Directory.SetLastWriteTimeUtc(full, time.UtcDateTime);
            }
            else
            {
                File.SetLastWriteTimeUtc(full, time.UtcDateTime);
            }
        }

        return root;
    }

    public void Dispose()
    {
        foreach (var directory in _directories)
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
