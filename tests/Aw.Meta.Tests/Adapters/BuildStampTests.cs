using Aw.Meta.Infrastructure.Build;

namespace Aw.Meta.Tests.Adapters;

public sealed class BuildStampTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("1.0.0+0123456789abcdef0123456789abcdef01234567", "0123456789abcdef0123456789abcdef01234567")]
    [InlineData("1.0.0-preview.1+abc123", "abc123")]
    [InlineData("1.0.0+abc+extra", "abc+extra")]
    [InlineData("1.0.0+0123456789abcdef0123456789abcdef01234567-dirty", "0123456789abcdef0123456789abcdef01234567-dirty")]
    public void The_commit_is_what_follows_the_first_plus(string version, string commit) => Assert.Equal(commit, BuildStamp.Commit(version));

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("1.0.0")]
    [InlineData("1.0.0+")]
    public void A_version_with_no_commit_is_unknown(string version) => Assert.Equal("unknown", BuildStamp.Commit(version));
}
