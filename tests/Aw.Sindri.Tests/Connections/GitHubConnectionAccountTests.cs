using System.Net;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Infrastructure.Connections.GitHub;
using Aw.Sindri.Testing.Connections;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Connections;

public sealed class GitHubConnectionAccountTests : IDisposable
{
    private readonly InMemoryConnectionStore _store = new();
    private readonly FakeConnectionSecrets _secrets = new();
    private readonly FakeTimeProvider _clock = new();
    private readonly Handler _handler = new();
    private readonly HttpClient _http;

    public GitHubConnectionAccountTests()
    {
        _http = new HttpClient(_handler);
    }

    public void Dispose() => _http.Dispose();

    private GitHubConnectionAccount Account() => new(_store, _secrets, new FakeConnectionPresets(), _http, _clock);

    private async Task Add(string label, ConnectionStatus status, string probe = "https://api.github.com/user")
    {
        var sample = ConnectionSamples.Oauth(ConnectionSamples.Name("github", label), status);
        var connection = sample with { Config = new Dictionary<string, string>(StringComparer.Ordinal) { ["probeUrl"] = probe } };
        _ = (await _store.Insert(connection, CancellationToken.None)).Value;
        _ = _secrets.Store(connection.Name, new TokenSet("gho_secret", RefreshToken: null, ExpiresAt: null)).Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Current_is_missing_when_there_is_no_github_connection()
    {
        var account = (await Account().Current(CancellationToken.None)).Value;

        Assert.Equal(GitHubAccountState.Missing, account.State);
        Assert.Null(account.Connection);
        Assert.Equal(GitHubLoginMiss.None, account.Miss);
        Assert.Equal(0, _handler.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Current_reads_the_login_from_user_with_the_connections_token()
    {
        await Add("work", ConnectionStatus.Connected);
        _handler.Answer = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"login":"joi-example","id":1}""") };

        var account = (await Account().Current(CancellationToken.None)).Value;

        Assert.Equal(new GitHubAccount(GitHubAccountState.Connected, "github:work", "joi-example"), account);
        Assert.Equal("Bearer gho_secret", _handler.Authorization);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Current_asks_github_once_while_the_connection_is_unchanged()
    {
        await Add("work", ConnectionStatus.Connected);
        _handler.Answer = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"login":"joi-example"}""") };
        var account = Account();

        _ = await account.Current(CancellationToken.None);
        _ = await account.Current(CancellationToken.None);

        Assert.Equal(1, _handler.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Current_is_connected_without_a_login_when_user_fails_and_does_not_ask_again_within_a_minute()
    {
        await Add("work", ConnectionStatus.Connected);
        _handler.Answer = new HttpResponseMessage(HttpStatusCode.Forbidden);
        var account = Account();

        var first = (await account.Current(CancellationToken.None)).Value;
        _ = await account.Current(CancellationToken.None);

        Assert.Equal(new GitHubAccount(GitHubAccountState.Connected, "github:work", Login: null, GitHubLoginMiss.Unreachable), first);
        Assert.Equal(1, _handler.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Current_never_sends_the_token_to_another_host()
    {
        await Add("work", ConnectionStatus.Connected, probe: "https://evil.example/user");

        var account = (await Account().Current(CancellationToken.None)).Value;

        Assert.Null(account.Login);
        Assert.Equal(GitHubLoginMiss.UnsupportedHost, account.Miss);
        Assert.Equal(0, _handler.Calls);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(HttpStatusCode.Unauthorized, GitHubLoginMiss.SignIn)]
    [InlineData(HttpStatusCode.ServiceUnavailable, GitHubLoginMiss.Unreachable)]
    [InlineData(HttpStatusCode.Forbidden, GitHubLoginMiss.Unreachable)]
    [InlineData(HttpStatusCode.TooManyRequests, GitHubLoginMiss.Unreachable)]
    public async Task Current_names_why_user_gave_no_login(HttpStatusCode status, GitHubLoginMiss miss)
    {
        await Add("work", ConnectionStatus.Connected);
        _handler.Answer = new HttpResponseMessage(status);

        var account = (await Account().Current(CancellationToken.None)).Value;

        Assert.Null(account.Login);
        Assert.Equal(miss, account.Miss);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Current_keeps_the_miss_with_the_cached_null_login()
    {
        await Add("work", ConnectionStatus.Connected);
        _handler.Answer = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        var account = Account();

        _ = await account.Current(CancellationToken.None);
        var second = (await account.Current(CancellationToken.None)).Value;

        Assert.Equal(GitHubLoginMiss.SignIn, second.Miss);
        Assert.Equal(1, _handler.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Current_says_sign_in_when_the_token_cannot_be_read()
    {
        await Add("work", ConnectionStatus.Connected);
        _secrets.Fails = true;

        var account = (await Account().Current(CancellationToken.None)).Value;

        Assert.Equal(GitHubLoginMiss.SignIn, account.Miss);
        Assert.Equal(0, _handler.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Current_prefers_a_connected_connection_over_one_that_needs_sign_in()
    {
        await Add("a-old", ConnectionStatus.NeedsSignIn);
        await Add("b-new", ConnectionStatus.Connected);
        _handler.Answer = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"login":"joi-example"}""") };

        var account = (await Account().Current(CancellationToken.None)).Value;

        Assert.Equal("github:b-new", account.Connection);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Current_names_the_connection_that_needs_sign_in()
    {
        await Add("work", ConnectionStatus.NeedsSignIn);

        var account = (await Account().Current(CancellationToken.None)).Value;

        Assert.Equal(new GitHubAccount(GitHubAccountState.NeedsSignIn, "github:work", Login: null, GitHubLoginMiss.SignIn), account);
        Assert.Equal(0, _handler.Calls);
    }

    private sealed class Handler : HttpMessageHandler
    {
        public HttpResponseMessage Answer { get; set; } = new(HttpStatusCode.NotFound);

        public int Calls { get; private set; }

        public string? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(Answer);
        }
    }
}
