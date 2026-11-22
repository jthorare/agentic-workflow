using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.TestWorkflow;

/// <summary>Starting a test run. A P0 contract stub (Workflows page spec §14): it refuses with SND-GRAPH-119 until its phase builds it.</summary>
internal sealed class TestWorkflowService : IService<TestWorkflowRequest, TestWorkflowResponse>
{
    public ValueTask<Result<TestWorkflowResponse>> Handle(TestWorkflowRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult<Result<TestWorkflowResponse>>(SindriErrors.GraphNotBuilt.With("Starting a test run is not built yet; the contract is in place"));
    }
}
