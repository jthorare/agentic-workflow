using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Features.CreateToolkit;

/// <summary>Makes a toolkit (see <see cref="CreateToolkitRequest"/>).</summary>
/// <param name="store">The toolkit store.</param>
/// <param name="folders">The toolkits root and its generator.</param>
/// <param name="jobs">The running job.</param>
/// <param name="callers">Who asked; the row records the caller's actor as its creator.</param>
internal sealed class CreateToolkitService(IToolkitStore store, IToolkitFolders folders, IJobContextAccessor jobs, ICallerContextAccessor callers) : IService<CreateToolkitRequest, CreateToolkitResult>
{
    public ValueTask<Result<CreateToolkitResult>> Handle(CreateToolkitRequest request, CancellationToken cancellationToken)
    {
        _ = (store, folders, jobs, callers);
        throw new NotSupportedException();
    }
}
