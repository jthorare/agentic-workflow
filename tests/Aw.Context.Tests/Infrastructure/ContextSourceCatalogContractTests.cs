using Aw.Context.Core.Docs;
using Aw.Context.Infrastructure.Linear;
using Aw.Context.Infrastructure.Sources;
using Aw.Context.Testing.Engine;
using Aw.Context.Tests.Onyx;

namespace Aw.Context.Tests.Infrastructure;

[Trait("Lane", "integration")]
public sealed class ContextSourceCatalogContractTests : SourceCatalogContract<ISourceCatalog>, IDisposable
{
    private readonly TempHome _home = new();
    private readonly HttpClient _http = new();

    public void Dispose()
    {
        _http.Dispose();
        _home.Dispose();
    }

    protected override ISourceCatalog Create() => new ContextSourceCatalog(_home.Database, _home.Git, _home.Clock,
        new LinearSources(new LinearSourceParts(_http, static _ => null, _home.Clock, new CapturingLogger<LinearClient>())));
}
