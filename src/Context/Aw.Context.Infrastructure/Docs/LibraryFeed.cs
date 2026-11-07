using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Docs;

/// <summary>The library record feed (review page spec 2026-11-04 §5.2): the design docs of the 'aw' root and the vaults.</summary>
#pragma warning disable MA0182 // why: the catalog builds it in the next commit
internal sealed class LibraryFeed(LibrarySetup setup, IRecordStore store, Scrubbers scrubbers, ILibraryStatus status, TimeProvider clock) : RecordFeed(LibrarySource.Id)
{
    public override ValueTask<Result<RecordPage>> Pull(RecordPull pull, CancellationToken cancellationToken)
    {
        _ = (setup, store, scrubbers, status, clock);
        throw new NotSupportedException();
    }
}
