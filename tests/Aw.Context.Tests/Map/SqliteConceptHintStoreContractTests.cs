using Aw.Context.Core.ConceptScan;
using Aw.Context.Infrastructure.ConceptScan;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Testing.Map;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Map;

[Trait("Lane", "integration")]
public sealed class SqliteConceptHintStoreContractTests : ConceptHintStoreContract, IDisposable
{
    private readonly TempHome _home = new();
    private readonly List<ContextDatabase> _databases = [];

    public void Dispose()
    {
        _databases.ForEach(d => d.Dispose());
        _home.Dispose();
    }

    protected override IConceptHintStore CreateStore()
    {
        var settings = SettingsFromEnvironment.Read(_home.Environment);
        var database = new ContextDatabase(settings, new FakeTimeProvider());
        _databases.Add(database);
        return new SqliteConceptHintStore(database);
    }
}
