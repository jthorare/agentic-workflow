using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Plan;

/// <summary>'line desk step.plan.check|evidence': the plan station's block Steps, run at the desk.</summary>
internal sealed class PlanDeskService : IService<PlanDeskRequest, LineDeskResponse>
{
    private const string Check = "step.plan.check";
    private const string Evidence = "step.plan.evidence";

    public ValueTask<Result<LineDeskResponse>> Handle(PlanDeskRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var facts = new LineFacts(request.Facts);
        return ValueTask.FromResult(request.Block switch
        {
            Check or "plan.check" => Result(LineDesk.Check(Check, PlanChecks.Run(facts, LineStage.Draft))),
            Evidence or "plan.evidence" => Assemble(facts, request.Context),
            _ => SindriErrors.InvalidInput.With($"the plan station has no desk code for '{request.Block}'; it covers {Check} and {Evidence}"),
        });
    }

    private static Result<LineDeskResponse> Result(LineDeskResponse response) => response;

    private static Result<LineDeskResponse> Assemble(LineFacts facts, LineDeskContext context)
    {
        var checks = PlanChecks.Run(facts, LineStage.Evidence);
        if (!checks.Passed)
        {
            return LineDesk.Incomplete(Evidence, checks);
        }

        if (context.Identity is null || string.IsNullOrEmpty(context.ScopeHash) || string.IsNullOrEmpty(context.DesignHash))
        {
            return SindriErrors.InvalidInput.With("the evidence Step assembles the packet and needs its identity and the hashes of the design and scope packets (fix: pass --packet-id, --repo-slug, --base-sha, --scope-hash and --design-hash, or leave packet.json, scopePacket.json and designPacket.json in the facts directory)");
        }

        var parents = new PlanParents(context.DesignHash, context.ScopeHash);
        return LineDesk.Complete(Evidence, checks, "plan-packet", PlanPacket.Build(facts, context.Identity, parents, new PlanGraphOptions(context.Parallel, context.MaxStack, context.DefaultBranch)));
    }
}
