using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Features.ResumeToolkit;

/// <summary>Resumes a toolkit (see <see cref="ResumeToolkitRequest"/>).</summary>
/// <param name="store">The toolkit store.</param>
/// <param name="runtime">Owns the process.</param>
internal sealed class ResumeToolkitService(IToolkitStore store, IToolkitRuntime runtime) : IService<ResumeToolkitRequest, ResumeToolkitResponse>
{
    public ValueTask<Result<ResumeToolkitResponse>> Handle(ResumeToolkitRequest request, CancellationToken cancellationToken)
    {
        _ = (store, runtime);
        throw new NotSupportedException();
    }
}
