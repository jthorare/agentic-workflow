using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Testing.Map;
using Xunit;

namespace Aw.Context.Testing.Extract;

/// <summary>The repo-remote port's contract (#1702): the fake and the git adapter pass the same tests; the origin comes back as written, a checkout without one has none, and a checkout that can't be read is SND-CONTEXT-048.</summary>
public abstract class RepoRemoteContract
{
    /// <summary>The origin the contract sets on a repo that has one.</summary>
    protected const string OriginUrl = "git@github.com:acme/aw-split-panes.git";

    private static CancellationToken None => TestContext.Current.CancellationToken;

    /// <summary>What the checkout has.</summary>
    protected enum Standing
    {
        /// <summary>An origin remote set to <see cref="OriginUrl"/>.</summary>
        HasOrigin,

        /// <summary>A readable checkout with no origin remote.</summary>
        NoOrigin,

        /// <summary>The checkout can't be read.</summary>
        Unreadable,
    }

    /// <summary>Builds the port over a repo in the given standing.</summary>
    /// <param name="standing">What the checkout has.</param>
    /// <param name="repo">The repo the port is asked about.</param>
    /// <returns>The port.</returns>
    protected abstract IRepoRemote Create(Standing standing, out ContextRepo repo);

    /// <summary>The origin comes back as written.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_origin_is_the_url_the_checkout_has()
    {
        const string test = nameof(The_origin_is_the_url_the_checkout_has);
        var remote = Create(Standing.HasOrigin, out var repo);

        Expect.That(Expect.Ok(await remote.Origin(repo, None), test) == OriginUrl, test, "the origin wasn't the one set");
    }

    /// <summary>No origin is null, not an error.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_checkout_with_no_origin_answers_null()
    {
        const string test = nameof(A_checkout_with_no_origin_answers_null);
        var remote = Create(Standing.NoOrigin, out var repo);

        Expect.That(Expect.Ok(await remote.Origin(repo, None), test) is null, test, "a repo with no origin had one");
    }

    /// <summary>A checkout that can't be read is SND-CONTEXT-048.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_unreadable_checkout_is_unreadable()
    {
        const string test = nameof(An_unreadable_checkout_is_unreadable);
        var remote = Create(Standing.Unreadable, out var repo);

        var origin = await remote.Origin(repo, None);

        Expect.That(origin.IsError && origin.Error.Code == "SND-CONTEXT-048", test, origin.IsError ? origin.Error.Code : "it answered");
    }
}
