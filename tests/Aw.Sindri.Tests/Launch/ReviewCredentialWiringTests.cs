using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Infrastructure.Launch;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Launch;

/// <summary>LauncherServiceCollectionExtensions.ReviewCredentials: the variables the live and the approved profile's 'review.credential: env:NAME' read; a profile that can't be read doesn't stop the other one's.</summary>
public sealed class ReviewCredentialWiringTests
{
    private static readonly ProfileHash _hash = ProfileHash.From(new string('a', 64));

    private static async Task<IReadOnlyList<string>> Variables(Result<LoadedProfile?> approved, Result<LoadedProfile> live)
    {
        await using var sp = new ServiceCollection().AddSingleton<IApprovedProfile>(new Approved(approved)).AddSingleton<IProfileSource>(new Live(live)).BuildServiceProvider();
        return await LauncherServiceCollectionExtensions.ReviewCredentials(sp)(CancellationToken.None);
    }

    private static LoadedProfile Of(PrReviewSettings? review) => new(new SindriProfile(1, "thor", Review: review), _hash, "profile.yaml");

    private static Result<LoadedProfile?> ApprovedOf(PrReviewSettings? review) => Of(review);

    private static Result<LoadedProfile> LiveOf(PrReviewSettings? review) => Of(review);

    private static Result<LoadedProfile?> UnreadableApproved() => Core.SindriErrors.ApprovalsUnavailable.With("unreadable");

    private static Result<LoadedProfile> UnreadableLive() => Core.SindriErrors.ProfileInvalid.With("unreadable");

    [Fact, Trait("Lane", "unit")]
    public async Task An_approved_env_credential_names_its_variable() =>
        Assert.Equal(["MY_REVIEW_TOKEN"], await Variables(ApprovedOf(new PrReviewSettings(Credential: "env:MY_REVIEW_TOKEN")), LiveOf(review: null)));

    [Fact, Trait("Lane", "unit")]
    public async Task An_unapproved_live_env_credential_is_scrubbed_too() =>
        Assert.Equal(["LIVE_ONLY_TOKEN"], await Variables(ApprovedOf(review: null), LiveOf(new PrReviewSettings(Credential: "env:LIVE_ONLY_TOKEN"))));

    [Fact, Trait("Lane", "unit")]
    public async Task Both_names_are_scrubbed_once_each()
    {
        var both = await Variables(ApprovedOf(new PrReviewSettings(Credential: "env:OLD_TOKEN")), LiveOf(new PrReviewSettings(Credential: "env:NEW_TOKEN")));
        Assert.Equal(["OLD_TOKEN", "NEW_TOKEN"], both);
        Assert.Equal(["SAME_TOKEN"], await Variables(ApprovedOf(new PrReviewSettings(Credential: "env:SAME_TOKEN")), LiveOf(new PrReviewSettings(Credential: "env:SAME_TOKEN"))));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_command_credential_names_none() =>
        Assert.Empty(await Variables(ApprovedOf(new PrReviewSettings(Credential: "command:gh auth token")), LiveOf(new PrReviewSettings(Credential: "command:gh auth token"))));

    [Fact, Trait("Lane", "unit")]
    public async Task No_review_block_or_no_credential_names_none()
    {
        Assert.Empty(await Variables(ApprovedOf(review: null), LiveOf(review: null)));
        Assert.Empty(await Variables(ApprovedOf(new PrReviewSettings(Login: "joi-example")), LiveOf(new PrReviewSettings(Login: "joi-example"))));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Unreadable_approvals_still_scrub_the_live_name() =>
        Assert.Equal(["LIVE_ONLY_TOKEN"], await Variables(UnreadableApproved(), LiveOf(new PrReviewSettings(Credential: "env:LIVE_ONLY_TOKEN"))));

    [Fact, Trait("Lane", "unit")]
    public async Task An_unreadable_live_profile_still_scrubs_the_approved_name() =>
        Assert.Equal(["MY_REVIEW_TOKEN"], await Variables(ApprovedOf(new PrReviewSettings(Credential: "env:MY_REVIEW_TOKEN")), UnreadableLive()));

    [Fact, Trait("Lane", "unit")]
    public async Task Two_unreadable_profiles_name_none() =>
        Assert.Empty(await Variables(UnreadableApproved(), UnreadableLive()));

    private sealed class Approved(Result<LoadedProfile?> answer) : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken) => ValueTask.FromResult(answer);
    }

    private sealed class Live(Result<LoadedProfile> answer) : IProfileSource
    {
        public ValueTask<Result<LoadedProfile>> Load(CancellationToken cancellationToken) => ValueTask.FromResult(answer);
    }
}
