using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Infrastructure.Launch;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Launch;

/// <summary>LauncherServiceCollectionExtensions.ReviewCredential: the variable the approved profile's 'review.credential: env:NAME' reads, else null.</summary>
public sealed class ReviewCredentialWiringTests
{
    private static readonly ProfileHash _hash = ProfileHash.From(new string('a', 64));

    private static async Task<string?> Variable(Result<LoadedProfile?> approved)
    {
        await using var sp = new ServiceCollection().AddSingleton<IApprovedProfile>(new Approved(approved)).BuildServiceProvider();
        return await LauncherServiceCollectionExtensions.ReviewCredential(sp)(CancellationToken.None);
    }

    private static Result<LoadedProfile?> With(PrReviewSettings? review) =>
        new LoadedProfile(new SindriProfile(1, "thor", Review: review), _hash, "profile.yaml");

    [Fact, Trait("Lane", "unit")]
    public async Task An_env_credential_names_its_variable() =>
        Assert.Equal("MY_REVIEW_TOKEN", await Variable(With(new PrReviewSettings(Credential: "env:MY_REVIEW_TOKEN"))));

    [Fact, Trait("Lane", "unit")]
    public async Task A_command_credential_names_none() =>
        Assert.Null(await Variable(With(new PrReviewSettings(Credential: "command:gh auth token"))));

    [Fact, Trait("Lane", "unit")]
    public async Task No_review_block_or_no_credential_names_none()
    {
        Assert.Null(await Variable(With(review: null)));
        Assert.Null(await Variable(With(new PrReviewSettings(Login: "joi-example"))));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unreadable_profile_names_none() =>
        Assert.Null(await Variable(Unreadable()));

    private static Result<LoadedProfile?> Unreadable() => Core.SindriErrors.ApprovalsUnavailable.With("unreadable");

    private sealed class Approved(Result<LoadedProfile?> answer) : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken) => ValueTask.FromResult(answer);
    }
}
