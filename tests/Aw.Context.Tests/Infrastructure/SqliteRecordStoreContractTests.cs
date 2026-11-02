using Aw.Context.Core.Records;
using Aw.Context.Infrastructure.Records;
using Aw.Context.Testing.Records;

namespace Aw.Context.Tests.Infrastructure;

/// <summary>The record-store contract against the real store on a temp context.db: what the in-memory fake promises, SQLite keeps.</summary>
[Trait("Lane", "integration")]
public sealed class SqliteRecordStoreContractTests : RecordStoreContract<IRecordStore>, IDisposable
{
    private readonly TempHome _home = new();

    protected override IRecordStore Create() => new SqliteRecordStore(_home.Database);

    public void Dispose() => _home.Dispose();
}
