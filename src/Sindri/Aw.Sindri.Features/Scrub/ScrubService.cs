using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.Scrub;

/// <summary>Red until cutover unit 4.1: redacts a text through Aw.Redaction, scans the added lines of the staged diff, or installs the pre-commit hook.</summary>
internal sealed class ScrubService : IService<ScrubRequest, ScrubResponse>
{
    public ValueTask<Result<ScrubResponse>> Handle(ScrubRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("scrub is built in cutover unit 4.1");
}
