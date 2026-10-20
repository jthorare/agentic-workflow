using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Context.Core.Proposals;
using Aw.Context.Core.Review;
using Aw.Context.Features.CleanMap;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features;

/// <summary>The map part of the facade: each method goes to the group of slices that owns it (spec §4); the groups keep each class under the constructor limit.</summary>
internal sealed class ContextMap(MapAdmin admin, MapReview review, MapProposals proposals, IService<CleanMapRequest, CleanupReport> clean) : IContextMap
{
    public ValueTask<Result<MapFilePreview>> PreviewMap(string path, ContextScope scope, CancellationToken cancellationToken) =>
        admin.PreviewMap(path, scope, cancellationToken);

    public ValueTask<Result<MapImported>> ImportMap(string path, string confirmedHash, ContextScope scope, CancellationToken cancellationToken) =>
        admin.ImportMap(path, confirmedHash, scope, cancellationToken);

    public ValueTask<Result<MapExported>> ExportMap(string? path, ContextScope scope, CancellationToken cancellationToken) =>
        admin.ExportMap(path, scope, cancellationToken);

    public ValueTask<Result<MapStatusView>> MapStatus(string? anchor, ContextScope scope, CancellationToken cancellationToken) => admin.MapStatus(anchor, scope, cancellationToken);

    public ValueTask<Result<MapHealth>> CheckMap(ContextScope? scope, CancellationToken cancellationToken) => admin.CheckMap(scope, cancellationToken);

    public ValueTask<Result<MapHealth>> CheckRetention(CancellationToken cancellationToken) => admin.CheckRetention(cancellationToken);

    public ValueTask<Result<PurgeReport>> PurgeExpired(CancellationToken cancellationToken) => admin.PurgeExpired(cancellationToken);

    public ValueTask<Result<MapBuildReport>> BuildMap(IProgress<string>? progress, ContextScope scope, CancellationToken cancellationToken) =>
        proposals.BuildMap(progress, scope, cancellationToken);

    public ValueTask<Result<ReviewQueueView>> ReviewQueue(int? limit, ContextScope scope, CancellationToken cancellationToken) => review.ReviewQueue(limit, scope, cancellationToken);

    public ValueTask<Result<ReviewResult>> ApplyReview(ReviewDecision decision, ContextScope scope, CancellationToken cancellationToken) =>
        review.ApplyReview(decision, scope, cancellationToken);

    public ValueTask<Result<ReviewHistoryView>> ReviewHistory(int? limit, ContextScope scope, CancellationToken cancellationToken) => review.ReviewHistory(limit, scope, cancellationToken);

    public ValueTask<Result<ReviewExported>> ExportReview(string? path, ContextScope scope, CancellationToken cancellationToken) =>
        review.ExportReview(path, scope, cancellationToken);

    public ValueTask<Result<ReviewFilePreview>> PreviewReviewFile(string path, ContextScope scope, CancellationToken cancellationToken) =>
        review.PreviewReviewFile(path, scope, cancellationToken);

    public ValueTask<Result<ReviewResult>> ApplyReviewFile(string path, ContextScope scope, CancellationToken cancellationToken) =>
        review.ApplyReviewFile(path, scope, cancellationToken);

    public ValueTask<Result<MapPromoted>> Promote(ContextScope scope, CancellationToken cancellationToken) => review.Promote(scope, cancellationToken);

    public ValueTask<Result<CleanupReport>> CleanDraft(ContextScope scope, CancellationToken cancellationToken) =>
        clean.Handle(new CleanMapRequest(scope), cancellationToken);

    public ValueTask<Result<ProposeResult>> Propose(ScopeProposal proposal, ContextScope scope, CancellationToken cancellationToken) =>
        proposals.Propose(proposal, scope, cancellationToken);

    public ValueTask<Result<ProposalsView>> Proposals(string run, ContextScope scope, CancellationToken cancellationToken) =>
        proposals.Proposals(run, scope, cancellationToken);
}
