using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.CheckProfile;

/// <summary>The index writer (SetPages, PinConcepts) with a profile check after each write that succeeded.</summary>
internal sealed class RecheckingProfileIndexWriter(IProfileIndexWriter inner, ProfileRecheck recheck) : IProfileIndexWriter
{
    public async ValueTask<Result<ProfileHash>> Write(IReadOnlyList<IndexEdit> edits, ProfileHash? ifHash, CancellationToken cancellationToken)
    {
        var written = await inner.Write(edits, ifHash, cancellationToken).ConfigureAwait(false);
        if (written.IsSuccess)
        {
            await recheck.Now(cancellationToken).ConfigureAwait(false);
        }

        return written;
    }
}
