using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// One review action (context spec §8.7). Every variant acts at once, for a human or a session: one override row each (the bulk accept: one per
/// deterministic entry), recorded as 'context.map.reviewed'; apply-file, merge, add and delete no longer ask for a typed challenge
/// (each is a reversible override row; removed per owner q93, 2026-10-23).
/// </summary>
internal sealed class ReviewContextMapService(ContextScopes scopes, IContextEngine engine, HeldLease held, ILedgerStore ledger, EventFactory events) : IService<ReviewContextMapRequest, ReviewContextMapResponse>
{
    public async ValueTask<Result<ReviewContextMapResponse>> Handle(ReviewContextMapRequest request, CancellationToken cancellationToken)
    {
        var input = ReviewInput.Read(request);
        if (input.IsError)
        {
            return input.Error;
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        return await Act(request.Verb, input.Value, scope.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Performs the action and records 'context.map.reviewed' when it wrote rows.</summary>
    private async ValueTask<Result<ReviewContextMapResponse>> Act(string verb, ReviewInput input, ContextScope scope, CancellationToken cancellationToken)
    {
        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var done = input.Path is { } path
            ? await engine.ApplyReviewFile(path, scope, cancellationToken).ConfigureAwait(false)
            : await engine.ApplyReview(input.Decision!, scope, cancellationToken).ConfigureAwait(false);   // why: a non-file input carries its decision
        if (done.IsError || done.Value.Written.Count == 0)
        {
            return done.IsError ? done.Error : new ReviewContextMapResponse(done.Value, Sequence: null);
        }

        var payload = new ContextMapReviewed(verb, done.Value.Written.Count, done.Value.Undecided);
        var appended = await ledger.Append(events.Entry(EventTypes.ContextMapReviewed, payload, $"{EventTypes.ContextMapReviewed}:{done.Value.Written[0].Value}"), grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new ReviewContextMapResponse(done.Value, appended.Value.Sequence);
    }
}
