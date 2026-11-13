using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Features.ListReviews;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Tests.Views;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aw.Sindri.Tests.Reviews;

/// <summary>The Reviews list's read of the approved profile (#3435).</summary>
[Lane(TestLane.Unit)]
public sealed class ListReviewsServiceTests
{
    private readonly ViewsHarness _v = new();
    private readonly FakeGitHubAccount _account = new() { State = GitHubAccountState.Connected, Connection = "github:work", Login = "joi-example" };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ListReviewsService Service()
    {
        var forge = new FakePullRequests(["github.com"]);
        var summaries = new ReviewSummaryReader(new NoSummaries(), forge, _v.Slice.Clock, NullLogger<ReviewSummaryReader>.Instance);
        return new ListReviewsService(_v.Slice.Profiles, new FakeReviewFacts(), new FakeReviewProjects(), summaries, new ReviewAccountReader(_account, forge), _v.Progress, _v.Slice.Clock);
    }

    [Fact]
    public async Task An_invalid_live_profile_reads_the_list_like_no_profile()
    {
        _v.Slice.Profiles.Live = SindriErrors.ProfileInvalid.With("/home/thor/profile.yaml: line 3: unknown key 'tennant'");

        var result = await Service().Handle(new ListReviewsRequest(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Assert.Equal("joi-example", result.Value.Login);
        Assert.Null(result.Value.Connect);
    }

    private sealed class NoSummaries : IReviewSummaries
    {
        public ValueTask<Result<ReviewSummary?>> Read(ReviewPullRef pull, string sha, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success<ReviewSummary?>(value: null));

        public ValueTask<Result<ReviewSummary?>> Newest(ReviewPullRef pull, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success<ReviewSummary?>(value: null));

        public ValueTask<Result<Unit>> Write(ReviewPullRef pull, ReviewSummary summary, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success(Unit.Value));
    }
}
