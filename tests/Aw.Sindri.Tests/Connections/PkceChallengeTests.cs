using System.Text.RegularExpressions;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections.OAuth;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.Connections;

[Lane(TestLane.Unit)]
public sealed partial class PkceChallengeTests
{
    // RFC 7636 appendix B
    private const string RfcVerifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
    private const string RfcChallenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

    [GeneratedRegex("^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Base64Url { get; }

    [Fact]
    public void The_challenge_is_S256_of_the_verifier_and_the_state_is_checked_on_callback()
    {
        Assert.Equal(RfcChallenge, PkceChallenge.ChallengeFor(RfcVerifier));

        var pkce = PkceChallenge.Create(static n => [.. Enumerable.Range(0, n).Select(static i => (byte)(i + 1))]);

        Assert.Equal("S256", PkceChallenge.Method);
        Assert.Equal(PkceChallenge.ChallengeFor(pkce.Verifier), pkce.Challenge);
        Assert.True(pkce.Verifier.Length is >= 43 and <= 128);
        Assert.Matches(Base64Url, pkce.Verifier);
        Assert.Matches(Base64Url, pkce.Challenge);
        Assert.Matches(Base64Url, pkce.State);
        Assert.True(pkce.Verify(pkce.State));
        Assert.False(pkce.Verify(pkce.State + "x"));
        Assert.False(pkce.Verify("forged"));
        Assert.False(pkce.Verify(state: null));
        Assert.False(pkce.Verify(string.Empty));
    }

    [Fact]
    public void The_state_is_32_random_bytes_and_each_sign_in_gets_its_own()
    {
        var calls = new List<int>();
        var counter = 0;
        var first = PkceChallenge.Create(n =>
        {
            calls.Add(n);
            return [.. Enumerable.Repeat((byte)(++counter), n)];
        });
        var second = PkceChallenge.Create(n => [.. Enumerable.Repeat((byte)(++counter), n)]);

        Assert.Contains(32, calls);
        Assert.Equal(32, Convert.FromBase64String(first.State.Replace('-', '+').Replace('_', '/') + "=").Length);
        Assert.NotEqual(first.State, second.State, StringComparer.Ordinal);
        Assert.NotEqual(first.Verifier, second.Verifier, StringComparer.Ordinal);
    }

    [Fact]
    public void The_text_of_a_pair_shows_none_of_its_values()
    {
        var pkce = PkceChallenge.Create(static n => new byte[n]);

        Assert.DoesNotContain(pkce.Verifier, pkce.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(pkce.State, pkce.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_consent_url_carries_the_code_flow_the_challenge_and_the_state()
    {
        using var server = new FakeOAuthServer();
        var preset = server.Preset();
        var pkce = PkceChallenge.Create(static n => [.. Enumerable.Range(0, n).Select(static i => (byte)(i * 3))]);
        var request = new AuthorizationRequest(preset, "client-123", "http://127.0.0.1:41999/callback", ["read", "write"], pkce);

        var url = request.ToUrl();
        var query = System.Web.HttpUtility.ParseQueryString(url.Query);

        Assert.StartsWith(preset.AuthorizeUrl, url.GetLeftPart(UriPartial.Path), StringComparison.Ordinal);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("client-123", query["client_id"]);
        Assert.Equal("http://127.0.0.1:41999/callback", query["redirect_uri"]);
        Assert.Equal("read write", query["scope"]);
        Assert.Equal(pkce.State, query["state"]);
        Assert.Equal(pkce.Challenge, query["code_challenge"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Null(query["code_verifier"]);
        Assert.DoesNotContain(pkce.Verifier, url.ToString(), StringComparison.Ordinal);
    }
}
