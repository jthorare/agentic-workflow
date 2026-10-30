namespace Aw.Context.Core.Engine;

/// <summary>A page the owner named in the profile's 'index.pages' (pages spec §3.1), with its actions and the concepts each relates to.</summary>
/// <param name="Id">Its id, unique among the pages.</param>
/// <param name="Name">What the map shows.</param>
/// <param name="Repo">The repo it is in, or null to take the profile's only repo or the repo of its surface.</param>
/// <param name="SurfaceKey">The map surface it names ('surface:route/GET:/schedule/week'), or null for an owner page.</param>
/// <param name="Actions">Its actions, in profile order.</param>
public sealed record PagePin(PageId Id, string Name, ContextRepoName? Repo, string? SurfaceKey, IReadOnlyList<PageActionPin> Actions);
