using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Design;

/// <summary>'line desk step.design.check|evidence': the design station's block Steps, run at the desk.</summary>
internal sealed class DesignDeskService : IService<DesignDeskRequest, LineDeskResponse>
{
    private const string Check = "step.design.check";
    private const string Evidence = "step.design.evidence";

    public ValueTask<Result<LineDeskResponse>> Handle(DesignDeskRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var facts = new LineFacts(request.Facts);
        return ValueTask.FromResult(request.Block switch
        {
            Check or "design.check" => Result(LineDesk.Check(Check, DesignPacketChecks.Run(facts, LineStage.Draft))),
            Evidence or "design.evidence" => Assemble(facts, request.Context),
            _ => SindriErrors.InvalidInput.With($"the design station has no desk code for '{request.Block}'; it covers {Check} and {Evidence}"),
        });
    }

    private static Result<LineDeskResponse> Result(LineDeskResponse response) => response;

    private static Result<LineDeskResponse> Assemble(LineFacts facts, LineDeskContext context)
    {
        var checks = DesignPacketChecks.Run(facts, LineStage.Evidence);
        if (!checks.Passed)
        {
            return LineDesk.Incomplete(Evidence, checks);
        }

        if (context.Identity is null || string.IsNullOrEmpty(context.ScopeHash))
        {
            return SindriErrors.InvalidInput.With("the evidence Step assembles the packet and needs its identity and the scope packet's hash (fix: pass --packet-id, --repo-slug, --base-sha and --scope-hash, or leave packet.json and scopePacket.json in the facts directory)");
        }

        return LineDesk.Complete(Evidence, checks, "design-packet", DesignPacket.Build(facts, context.Identity, context.ScopeHash));
    }
}
