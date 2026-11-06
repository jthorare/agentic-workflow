using Aw.Context.Core.Linear;
using Aw.Context.Infrastructure.Records;
using Aw.Context.Testing.Records;

namespace Aw.Context.Tests.Infrastructure;

/// <summary>The pickers' store contract against the real store on a temp context.db (Linear hub spec 2026-11-04 §4.2): what the in-memory fake promises, SQLite keeps.</summary>
[Trait("Lane", "integration")]
public sealed class SqliteLinearLookupStoreContractTests : LinearLookupStoreContract<ILinearLookupStore>, IDisposable
{
    private readonly List<RecordStoreWorld> _worlds = [];

    public void Dispose()
    {
        foreach (var world in _worlds)
        {
            world.Dispose();
        }
    }

    protected override ILinearLookupStore Create()
    {
        var world = new RecordStoreWorld();
        _worlds.Add(world);
        return new SqliteLinearLookupStore(world.Database);
    }
}
