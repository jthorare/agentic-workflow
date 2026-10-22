using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.InitProfile;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.InitProfile;

/// <summary>'profile init' (setup-once spec §5.1): the default profile is written with the effects the shipped recipes declare, and the file's own refusal comes through.</summary>
[Lane(TestLane.Unit)]
public sealed class InitProfileServiceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_default_profile_is_created_with_the_shipped_effects_and_its_path_comes_back()
    {
        var initializer = new Initializer();

        var done = await new InitProfileService(initializer, new FakeShippedEffects("effect.claim.ref", "effect.notify.desk")).Handle(new InitProfileRequest(), Token);

        Assert.True(done.IsSuccess, done.IsError ? done.Error.Message : null);
        Assert.Equal("/home/profile.yaml", done.Value.Path);
        Assert.Equal(["effect.claim.ref", "effect.notify.desk"], initializer.Effects);
    }

    [Fact]
    public async Task A_profile_that_is_already_there_is_the_initializers_refusal()
    {
        var initializer = new Initializer { Fails = SindriErrors.ProfileInvalid.With("a profile already exists") };

        var done = await new InitProfileService(initializer, new FakeShippedEffects()).Handle(new InitProfileRequest(), Token);

        Assert.Equal("SND-PROFILE-002", done.Error.Code);
    }

    private sealed class Initializer : IProfileInitializer
    {
        public IReadOnlyList<string>? Effects { get; private set; }

        public Error? Fails { get; init; }

        public ValueTask<Result<string>> Create(IReadOnlyCollection<string> defaultEffects, CancellationToken cancellationToken)
        {
            Effects = [.. defaultEffects];
            return ValueTask.FromResult<Result<string>>(Fails is { } error ? error : "/home/profile.yaml");
        }
    }
}
