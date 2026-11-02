using Aw.Context.Core.Records;
using Aw.Context.Core.Search;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Vectors;
using Aw.Context.Testing.Records;
using Aw.Context.Testing.Vectors;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Context.Tests.Sync;

/// <summary>The record store and an empty feed catalog for a test world that composes the context features, so the sync service resolves.</summary>
internal static class RecordFakes
{
    /// <summary>Adds a fake record store, a catalog with no feeds, a chunk embedder, a lexical leg with no chunks and the dense leg's query embedder, matrix and scan.</summary>
    /// <param name="services">The world's services.</param>
    /// <returns>The same services.</returns>
    public static IServiceCollection AddRecordFakes(this IServiceCollection services) =>
        services.AddSingleton<IRecordStore>(new FakeRecordStore()).AddSingleton<IRecordFeedCatalog>(new FakeRecordFeedCatalog()).AddSingleton<IChunkEmbedder>(new FakeChunkEmbedder())
            .AddSingleton<ILexicalLeg>(new FakeLexicalLeg()).AddSingleton<IQueryEmbedder>(new FakeQueryEmbedder()).AddSingleton<IVectorScan>(new FakeVectorScan())
            .AddSingleton<IVectorMatrixCache>(sp => new FakeVectorMatrixCache(sp.GetRequiredService<IRecordStore>())).AddSingleton<ISourceIdentityStore>(new FakeSourceIdentityStore());
}
