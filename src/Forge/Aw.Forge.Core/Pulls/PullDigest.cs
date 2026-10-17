using Aw.Forge.Core.Refs;

namespace Aw.Forge.Core.Pulls;

/// <summary>The cheap fields the poller compares between polls.</summary>
/// <param name="Ref">The pull request.</param>
/// <param name="Status">Open, closed or merged.</param>
/// <param name="HeadSha">The head tip.</param>
/// <param name="Checks">The checks state.</param>
/// <param name="CommentCount">Comments, review comments and reviews together.</param>
/// <param name="Version">An opaque stamp that changes when anything on the pull request changes.</param>
public sealed record PullDigest(ForgeRef Ref, PullStatus Status, string HeadSha, ChecksState Checks, int CommentCount, string Version);
