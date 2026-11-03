using Aw.Judge.Infrastructure.Providers;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

/// <summary>AW_JUDGE_JEV_URL points Jev at a stand-in on this machine, and nowhere else.</summary>
[Lane(TestLane.Unit)]
public sealed class JevEndpointTests
{
    [Theory]
    [InlineData("http://127.0.0.1:8080/v1/systemone")]
    [InlineData("http://localhost:9/v1/systemone")]
    [InlineData("http://[::1]:7000/v1/systemone")]
    [InlineData("https://127.0.0.1/v1/systemone")]
    public void A_loopback_http_url_is_used_without_a_warning(string value)
    {
        List<string> warnings = [];

        var uri = JevEndpoint.Resolve(value, warnings.Add);

        Assert.Equal(new Uri(value), uri);
        Assert.Empty(warnings);
    }

    [Theory]
    [InlineData("https://api.typesafe.ai/v1/systemone")]
    [InlineData("http://192.168.1.5:8080/")]
    [InlineData("http://127.0.0.1.evil.example/")]
    [InlineData("ftp://127.0.0.1/")]
    [InlineData("not a url")]
    public void Any_other_value_is_ignored_with_one_warning(string value)
    {
        List<string> warnings = [];

        var uri = JevEndpoint.Resolve(value, warnings.Add);

        Assert.Null(uri);
        Assert.Equal("AW_JUDGE_JEV_URL is not a loopback URL; using the real Jev", Assert.Single(warnings));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void An_unset_variable_is_the_real_jev_and_silent(string? value)
    {
        List<string> warnings = [];

        Assert.Null(JevEndpoint.Resolve(value, warnings.Add));
        Assert.Empty(warnings);
    }
}
