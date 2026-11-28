using Aw.Judge.Core.Ports;
using Aw.Judge.Infrastructure.Traits;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.CheckTraits;

[Lane(TestLane.Integration)]
public sealed class SqliteTraitVerdictCacheContractTests : TraitVerdictCacheContract, IDisposable
{
    private readonly TempStateDirectory _state = new();

    protected override ITraitVerdictCache Create(TimeProvider clock, TimeSpan keep, int maxEntries) =>
        new SqliteTraitVerdictCache(Path.Combine(_state.Path, "trait-check", SqliteTraitVerdictCache.FileName), clock, keep, maxEntries);

    // A regular file where the cache's directory should be: the directory can't be made.
    protected override ITraitVerdictCache CreateUnavailable()
    {
        var blocker = Path.Combine(_state.Path, "blocked");
        File.WriteAllText(blocker, "not a directory");
        return new SqliteTraitVerdictCache(Path.Combine(blocker, SqliteTraitVerdictCache.FileName), TimeProvider.System, TimeSpan.FromDays(7), 10);
    }

    public void Dispose() => _state.Dispose();
}
