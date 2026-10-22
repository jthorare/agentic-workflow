using Aw.Sindri.Core.Forge;

namespace Aw.Sindri.Testing.Forge;

/// <summary>A pull-request port ready for a contract run.</summary>
/// <param name="Pulls">The port.</param>
/// <param name="Repo">A repository on an allowed host that the run may use.</param>
/// <param name="ForeignHost">A host the port's host list does not allow.</param>
/// <param name="SeedThread">Plays the reviewer: adds an inline thread with a body to the pull request at a link and returns its key.</param>
/// <param name="CanPush">Whether PushBranch works in this run without a real remote (the fake simulates it; the file forge goes through real git).</param>
public sealed record PullRequestsUnderTest(IPullRequests Pulls, PullRepo Repo, string ForeignHost, Func<string, string, string> SeedThread, bool CanPush);
