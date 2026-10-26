using Aw.Context.Core.Map;
using Aw.Context.Infrastructure.Graph;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Testing.Map;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Map;

[Trait("Lane", "integration")]
public sealed class SqliteMapRulesetStoreContractTests : MapRulesetStoreContract, IDisposable
{
    private readonly TempHome _home = new();
    private readonly List<ContextDatabase> _databases = [];

    public void Dispose()
    {
        _databases.ForEach(d => d.Dispose());
        _home.Dispose();
    }

    protected override IGraphStore CreateStore()
    {
        var settings = SettingsFromEnvironment.Read(_home.Environment);
        var database = new ContextDatabase(settings, new FakeTimeProvider());
        _databases.Add(database);
        return new SqliteGraphStore(database, settings);
    }
}
