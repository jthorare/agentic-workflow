using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>The contract every <see cref="IProfileSource"/> honours, run against the in-memory profiles and the YAML file.</summary>
public abstract class ProfileSourceContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The source under test.</summary>
    /// <returns>The source.</returns>
    protected abstract IProfileSource Create();

    /// <summary>Puts a valid profile for user 'thor' in place.</summary>
    protected abstract void PutValid();

    /// <summary>Puts a profile that breaks the schema in place.</summary>
    protected abstract void PutInvalid();

    /// <summary>Makes sure there is no profile.</summary>
    protected abstract void PutNone();

    /// <summary>A valid profile loads, checked, with its hash and source.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_valid_profile_loads()
    {
        var source = Create();
        PutValid();

        var loaded = (await source.Load(Token)).Value;

        Expect.Equal("thor", loaded.Profile.User);
        Expect.True(!string.IsNullOrEmpty(loaded.Source));
    }

    /// <summary>No profile is SND-PROFILE-001.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task No_profile_is_SND_PROFILE_001()
    {
        var source = Create();
        PutNone();

        Expect.Equal(SindriErrors.ProfileMissing.Code, (await source.Load(Token)).Error.Code);
    }

    /// <summary>A profile that breaks the schema is SND-PROFILE-002.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_invalid_profile_is_SND_PROFILE_002()
    {
        var source = Create();
        PutInvalid();

        Expect.Equal(SindriErrors.ProfileInvalid.Code, (await source.Load(Token)).Error.Code);
    }
}
