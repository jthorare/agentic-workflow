using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Testing.Prepare;

namespace Aw.Sindri.Tests.Prepare;

[Lane(TestLane.Unit)]
public sealed class InMemoryPrepareCacheContractTests : PrepareCacheContract
{
    protected override ValueTask<(IPrepareCache Port, string Source, string Target, Func<string, bool> Installed)> Scenario()
    {
        var cache = new InMemoryPrepareCache();
        return ValueTask.FromResult<(IPrepareCache, string, string, Func<string, bool>)>((cache, "/work/a", "/work/b", directory => cache.Restored.Any(r => string.Equals(r.Directory, directory, StringComparison.Ordinal))));
    }
}
