using Aw.Context.Core.Linear;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>One stored issue (Linear hub spec §3). Never stored, or outside the caller's audience, is SND-LINEAR-005. Names, link and PR come from the same facts as the list; a removed issue is its last stored copy with writes off.</summary>
/// <param name="facts">The stored issues and the viewer.</param>
/// <param name="links">The items and pull requests linked to issues.</param>
/// <param name="gate">Whether the caller may write.</param>
internal sealed class GetLinearIssueService(ILinearIssueFacts facts, ILinearItemLinks links, ILinearWriteGate gate) : IService<GetLinearIssueRequest, GetLinearIssueResponse>
{
    public async ValueTask<Result<GetLinearIssueResponse>> Handle(GetLinearIssueRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var source = await facts.Source(cancellationToken).ConfigureAwait(false);
        if (source.IsError)
        {
            return source.Error;
        }

        var detail = await facts.Detail(request.Key, cancellationToken).ConfigureAwait(false);
        if (detail.IsError)
        {
            return detail.Error;
        }

        return detail.Value is { } found ? await Sheet(found, source.Value.Viewer, cancellationToken).ConfigureAwait(false) : NotFound(request.Key);
    }

    private async ValueTask<GetLinearIssueResponse> Sheet(LinearIssueDetailFacts detail, LinearPersonView? viewer, CancellationToken cancellationToken)
    {
        var r = detail.Row;
        // why: a link read that fails must not hide the issue; the sheet then shows without its item and PR
        var linked = await links.For([r.Record], cancellationToken).ConfigureAwait(false);
        var link = linked.IsSuccess ? linked.Value.GetValueOrDefault(r.Record) : null;
        var write = detail.Removed ? new LinearWriteGateView(Allowed: false, Code: null) : View(await gate.Refusal(r.Team.Key, cancellationToken).ConfigureAwait(false));
        return new GetLinearIssueResponse(r.Key, r.Record, r.Url, r.Title, r.Team, r.State, r.Priority, r.Assignee, r.Project, r.Cycle, r.Labels, r.UpdatedAt, link?.Link, link?.Pr, viewer, detail.Description, detail.Comments, detail.Removed, write);
    }

    private static LinearWriteGateView View(Error? refusal) => new(refusal is null, refusal?.Code);

    private static Error NotFound(string key) =>
        LinearHubErrors.IssueNotFound.With($"{key.Trim().ToUpperInvariant()} isn't stored: it isn't synced yet, or it's outside the teams Sindri reads (fix: Open in Linear, or wait for the next sync)");
}
