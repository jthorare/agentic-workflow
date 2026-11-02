using Aw.Context.Core.Records;
using Aw.Context.Infrastructure.Records;
using Aw.Context.Testing.Records;

namespace Aw.Context.Tests.Infrastructure;

/// <summary>The source-identity contract against the real store on a temp context.db.</summary>
[Trait("Lane", "integration")]
public sealed class SqliteSourceIdentityStoreContractTests : SourceIdentityStoreContract<ISourceIdentityStore>, IDisposable
{
    private readonly TempHome _home = new();

    protected override ISourceIdentityStore Create() => new SqliteSourceIdentityStore(_home.Database);

    public void Dispose() => _home.Dispose();
}
