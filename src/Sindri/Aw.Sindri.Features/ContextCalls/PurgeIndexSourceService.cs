using Aw.Context.Core.Facade;
using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Counts, and on confirm deletes, one source's rows in the record store through the context engine's facade. The caller rule (a human at a terminal) is the catalog row's.</summary>
/// <param name="engine">Context's facade: its record-feed members are Counts and Purge.</param>
internal sealed class PurgeIndexSourceService(IContextEngine engine) : IService<PurgeIndexSourceRequest, IndexPurgeView>
{
    public async ValueTask<Result<IndexPurgeView>> Handle(PurgeIndexSourceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!SourceId.TryFrom(request.Source, out var source))
        {
            return SindriErrors.InvalidInput.With($"'{request.Source}' isn't a source id: kebab-case, such as 'github' (fix: sindri index status)");
        }

        var counts = await engine.Counts(source, cancellationToken).ConfigureAwait(false);
        if (counts.IsError || !request.Confirm)
        {
            return counts.IsError ? counts.Error : new IndexPurgeView(source.Value, counts.Value.Records, counts.Value.Chunks, counts.Value.Vectors, Purged: false);
        }

        var purged = await engine.Purge(source, cancellationToken).ConfigureAwait(false);
        return purged.IsError ? purged.Error : new IndexPurgeView(source.Value, counts.Value.Records, counts.Value.Chunks, counts.Value.Vectors, Purged: true);
    }
}
