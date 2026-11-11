using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.ReportDefect;

/// <summary>Files a toolkit defect as a GitHub issue, or comments on the one already open for it.</summary>
/// <param name="config">The team config: the issue repo, the daily cap, the repo directory and the meta lane.</param>
/// <param name="repo">Runs gh.</param>
/// <param name="values">Holds today's counts.</param>
/// <param name="log">The running log.</param>
/// <param name="owner">The owner's queue, for the FYI.</param>
/// <param name="clock">The current time.</param>
internal sealed class ReportDefectService(IMetaConfig config, RepoCommands repo, IKeyValueStore values, IRunningLog log, IOwnerStore owner, TimeProvider clock) : IService<ReportDefectRequest, ReportDefectResponse>
{
    public ValueTask<Result<ReportDefectResponse>> Handle(ReportDefectRequest request, CancellationToken cancellationToken)
    {
        _ = (config, repo, values, log, owner, clock);   // red stub: the real body follows
        throw new NotSupportedException();
    }
}
