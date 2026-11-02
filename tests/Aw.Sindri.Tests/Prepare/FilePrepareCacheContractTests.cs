using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Testing.Prepare;
using Aw.Sindri.Tests.Sandbox.Egress;

namespace Aw.Sindri.Tests.Prepare;

/// <summary>The disk cache against a temp directory (a reflink where the filesystem has one, else the plain copy).</summary>
[Lane(TestLane.Integration)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class FilePrepareCacheContractTests : PrepareCacheContract, IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    protected override async ValueTask<(IPrepareCache Port, string Source, string Target, Func<string, bool> Installed)> Scenario()
    {
        var source = Path.Combine(_temp.Path, "source");
        _ = Directory.CreateDirectory(Path.Combine(source, "node_modules", "pkg"));
        await File.WriteAllTextAsync(Path.Combine(source, "node_modules", "pkg", "index.js"), "module.exports = 1;", TestContext.Current.CancellationToken);
        var target = Path.Combine(_temp.Path, "target");
        return (
            new FilePrepareCache(Path.Combine(_temp.Path, "cache"), new ProcessToolRunner()), source, target, directory => File.Exists(Path.Combine(directory, "node_modules", "pkg", "index.js")));
    }
}
