namespace Aw.Sindri.Core.Forge;

/// <summary>The cheap fields a poll compares between ticks, one per pull request.</summary>
/// <param name="Link">The pull request's link.</param>
/// <param name="State">Open, closed or merged.</param>
/// <param name="HeadSha">The head branch's tip now.</param>
/// <param name="Checks">The checks as one word.</param>
/// <param name="CommentCount">Comments, review comments and reviews together.</param>
/// <param name="Version">An opaque stamp that changes when anything on the pull request changes.</param>
public sealed record PullDigest(string Link, PullState State, string HeadSha, PullChecksState Checks, int CommentCount, string Version);
