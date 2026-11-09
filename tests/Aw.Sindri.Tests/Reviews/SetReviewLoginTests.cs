using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.SetReviewLogin;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Reviews;

/// <summary>SetReviewLogin: the save of 'review.login' records 'review.login-set' with the caller's actor, so a launched session's write is told apart from the owner's.</summary>
[Lane(TestLane.Unit)]
public sealed class SetReviewLoginTests
{
    private readonly SliceHarness _slice = new();
    private readonly InMemoryProfiles _profiles = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private SetReviewLoginService Service()
    {
        var profile = new SindriProfile(1, "o");
        _profiles.Live = new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml");
        return new SetReviewLoginService(_slice.Callers, new FakeKeyWriter(), new NoParser(), _profiles, _slice.Store, _slice.Held, _slice.Events);
    }

    [Fact]
    public async Task A_launched_session_saving_the_login_records_an_event_with_the_session_actor()
    {
        _ = await _slice.HoldLease();
        using var entered = _slice.Callers.Enter(A.Caller().Launched().Build());

        var result = await Service().Handle(new SetReviewLoginRequest("octocat"), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var recorded = Assert.Single(_slice.Store.Events);
        Expect.Equal((EventTypes.ReviewLoginSet, "session:launch:LCH-00000000000000000000000000000001"), (recorded.Envelope.Type, recorded.Envelope.Actor));
        Expect.Equal("octocat", _slice.Events.Payload<ReviewLoginSet>(recorded).Value.Login);
    }

    [Fact]
    public async Task The_owner_saving_the_login_records_a_human_actor()
    {
        _ = await _slice.HoldLease();
        using var entered = _slice.Callers.Enter(A.Caller().Human().Build());

        var result = await Service().Handle(new SetReviewLoginRequest("octocat"), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Expect.Equal("human:http", Assert.Single(_slice.Store.Events).Envelope.Actor);
    }

    private sealed class FakeKeyWriter : IProfileKeyWriter
    {
        public Result<ProfileKeyEdit> Plan(string keyPath, string value) => new ProfileKeyEdit("/home/profile.yaml", value, Changed: false);

        public Result<ProfileKeyEdit> PlanText(string keyPath, string value) => new ProfileKeyEdit("/home/profile.yaml", value, Changed: false);

        public Result<ProfileKeyEdit> PlanTexts(IReadOnlyList<(string KeyPath, string Value)> values) => throw new NotSupportedException();

        public Result<ProfileKeyEdit> PlanRemove(string keyPath) => throw new NotSupportedException();

        public Result<string> Save(ProfileKeyEdit edit) => edit.Path;
    }

    private sealed class NoParser : IProfileTextParser
    {
        public Result<LoadedProfile> Parse(string yaml, string source) => throw new NotSupportedException();

        public Result<LoadedProfile> Recheck(SindriProfile profile, string source) => throw new NotSupportedException();
    }
}
