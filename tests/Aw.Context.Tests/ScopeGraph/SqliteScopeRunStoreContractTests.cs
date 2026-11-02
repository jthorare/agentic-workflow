using Aw.Context.Core.ScopeGraph;
using Aw.Context.Infrastructure.ScopeRuns;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Testing.ScopeGraph;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.ScopeGraph;

[Trait("Lane", "integration")]
public sealed class SqliteScopeRunStoreContractTests : ScopeRunStoreContract, IDisposable
{
    private readonly Map.TempHome _home = new();
    private readonly List<ContextDatabase> _databases = [];

    public void Dispose()
    {
        _databases.ForEach(d => d.Dispose());
        _home.Dispose();
    }

    protected override IScopeRunStore CreateStore()
    {
        var database = new ContextDatabase(SettingsFromEnvironment.Read(_home.Environment), new FakeTimeProvider());
        _databases.Add(database);
        return new SqliteScopeRunStore(database);
    }
}
