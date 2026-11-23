using Aw.Context.Core.Concepts;
using Aw.Context.Core.Map;
using Aw.Context.Infrastructure.ConceptAnswers;
using Aw.Context.Infrastructure.Graph;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Testing.Concepts;
using Aw.Context.Tests.Map;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Concepts;

[Trait("Lane", "integration")]
public sealed class SqliteConceptAnswerStoreContractTests : ConceptAnswerStoreContract, IDisposable
{
    private readonly TempHome _home = new();
    private readonly List<ContextDatabase> _databases = [];

    public void Dispose()
    {
        _databases.ForEach(d => d.Dispose());
        _home.Dispose();
    }

    protected override (IGraphStore Graphs, IConceptAnswerStore Answers) Create()
    {
        var settings = SettingsFromEnvironment.Read(_home.Environment);
        var database = new ContextDatabase(settings, new FakeTimeProvider());
        _databases.Add(database);
        return (new SqliteGraphStore(database, settings), new SqliteConceptAnswerStore(database, new FakeTimeProvider()));
    }
}
