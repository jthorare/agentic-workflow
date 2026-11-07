using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Features.GetReviewPage;

/// <summary>What one read of the forge gave: the head, the files and the threads, and whether the head moved while they were read.</summary>
/// <param name="View">The pull request as the last head read saw it.</param>
/// <param name="Files">The files the PR changes.</param>
/// <param name="Threads">The review threads.</param>
/// <param name="Moving">True when the head moved twice, so these are the newest reads of a PR still moving.</param>
internal sealed record ReviewPageReads(PullView View, ReviewPageRead<PullFiles> Files, ReviewPageRead<IReadOnlyList<PullThread>> Threads, bool Moving);
