using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Profile;

/// <summary>A read surface sees an invalid live profile as no profile (#3435); every other error still fails the read.</summary>
[Lane(TestLane.Unit)]
public sealed class ApprovedProfileReadsTests
{
    private readonly InMemoryProfiles _profiles = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_invalid_live_profile_reads_as_null()
    {
        _profiles.Live = SindriErrors.ProfileInvalid.With("/home/thor/profile.yaml: line 3: unknown key 'tennant'");

        var read = await _profiles.ForRead(Token);

        Assert.True(read.IsSuccess);
        Assert.Null(read.Value);
    }

    [Fact]
    public async Task A_missing_profile_reads_as_null()
    {
        var read = await _profiles.ForRead(Token);

        Assert.True(read.IsSuccess);
        Assert.Null(read.Value);
    }

    [Fact]
    public async Task Any_other_error_stays_an_error()
    {
        _profiles.Live = SindriErrors.ProfileHashMismatch.With("the file changed under the read");

        var read = await _profiles.ForRead(Token);

        Assert.True(read.IsError);
        Assert.Equal(SindriErrors.ProfileHashMismatch.Code, read.Error.Code);
    }

    [Fact]
    public async Task A_valid_profile_passes_through()
    {
        var profile = SliceHarness.Profile();
        _profiles.Live = profile;

        var read = await _profiles.ForRead(Token);

        Assert.True(read.IsSuccess);
        Assert.Same(profile, read.Value);
    }
}
