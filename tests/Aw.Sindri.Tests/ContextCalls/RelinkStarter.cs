using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The RelinkPinAliases start the tick makes, counted.</summary>
internal sealed class RelinkStarter(SliceHarness harness) : IService<RelinkPinAliasesRequest, JobStartedResponse>
{
    internal List<string> Actors { get; } = [];

    public ValueTask<Result<JobStartedResponse>> Handle(RelinkPinAliasesRequest request, CancellationToken cancellationToken)
    {
        Actors.Add(harness.Callers.Current?.Actor ?? string.Empty);
        return ValueTask.FromResult<Result<JobStartedResponse>>(new JobStartedResponse(new CommandAccepted(harness.Ids.New<CommandId>(), 1), JobId.New(harness.Ids), new JobLinks("/j", "/c", "/e")));
    }
}
