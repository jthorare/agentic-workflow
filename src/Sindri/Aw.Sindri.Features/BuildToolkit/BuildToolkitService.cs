using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Locks;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Features.BuildToolkit;

/// <summary>Builds and tests a toolkit (see <see cref="BuildToolkitRequest"/>).</summary>
/// <param name="store">The toolkit store.</param>
/// <param name="workspace">The toolkit folders.</param>
/// <param name="commands">Runs the build and the tests.</param>
/// <param name="slots">The heavy-job slots.</param>
/// <param name="jobs">The running job.</param>
/// <param name="clock">The clock.</param>
internal sealed class BuildToolkitService(IToolkitStore store, IToolkitWorkspace workspace, IToolkitCommands commands, IHeavySlots slots, IJobContextAccessor jobs, TimeProvider clock)
    : IService<BuildToolkitRequest, BuildToolkitResult>
{
    public ValueTask<Result<BuildToolkitResult>> Handle(BuildToolkitRequest request, CancellationToken cancellationToken)
    {
        _ = (store, workspace, commands, slots, jobs, clock);   // the red stub: nothing is built yet
        throw new NotSupportedException();
    }
}
