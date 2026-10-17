using System.Net;
using Aw.Forge.Core;
using Aw.Forge.Core.Pulls;
using Aw.Forge.Core.Refs;
using Aw.Forge.Infrastructure.Credentials;
using Aw.Forge.Infrastructure.Git;
using Aw.Forge.Infrastructure.GitHub;
using Aw.Kernel;
using Aw.Redaction;

namespace Aw.Forge.Tests.GitHub;

/// <summary>GitHubForge over a scripted HTTP handler: the transport, auth and error mapping, never the real API.</summary>
[Trait("Lane", "unit")]
public sealed class GitHubForgeTests
{
    private const string Secret = "sekret-value-0001";
    private static readonly ForgeRepo _repo = new("github.com", "acme", "widgets");

    private static ForgeRef Pull(string host = "github.com") => new(ForgeKind.GitHub, host, "acme", "widgets", 7);

    private static (GitHubForge Forge, StubHandler Http, CountingCredentials Credentials) Build(StubHandler? http = null, string? token = Secret, params string[] hosts)
    {
        var handler = http ?? new StubHandler();
        var credentials = new CountingCredentials(token);
        var allowed = new ForgeHosts(hosts.Length > 0 ? hosts : ["github.com"]);
        var forge = new GitHubForge(new GitHubApi(new HttpClient(handler), credentials, allowed), credentials, new GitPusher(Redactor.Default), Redactor.Default);
        return (forge, handler, credentials);
    }

    [Fact]
    public async Task A_host_outside_the_list_sends_nothing()
    {
        var (forge, http, _) = Build();

        var result = await forge.GetPull(Pull("evil.example"), TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal(ForgeErrors.HostNotAllowed.Code, result.Error.Code);
        Assert.Empty(http.Seen);
        Assert.Equal(0, forge.RequestsSent);
    }

    [Fact]
    public async Task A_read_goes_to_the_api_host_with_a_bearer_token_and_version_headers()
    {
        var http = new StubHandler().On(HttpMethod.Get, "/repos/acme/widgets", HttpStatusCode.OK, "{\"default_branch\":\"trunk\"}");
        var (forge, _, _) = Build(http);

        var result = await forge.DefaultBranch(_repo, TestContext.Current.CancellationToken);

        Assert.Equal("trunk", result.Value);
        var request = Assert.Single(http.Seen);
        Assert.Equal("https://api.github.com/repos/acme/widgets", request.Url.ToString());
        Assert.Equal("Bearer " + Secret, request.Authorization);
    }

    [Fact]
    public async Task An_enterprise_host_uses_its_own_api_path()
    {
        var http = new StubHandler().On(HttpMethod.Get, "/api/v3/repos/acme/widgets", HttpStatusCode.OK, "{\"default_branch\":\"main\"}");
        var (forge, _, _) = Build(http, Secret, "git.corp.test");

        var result = await forge.DefaultBranch(new ForgeRepo("git.corp.test", "acme", "widgets"), TestContext.Current.CancellationToken);

        Assert.Equal("main", result.Value);
        Assert.Equal("https://git.corp.test/api/v3/repos/acme/widgets", Assert.Single(http.Seen).Url.ToString());
    }

    [Fact]
    public async Task A_missing_credential_is_AW_FORGE_003_and_sends_nothing()
    {
        var (forge, http, _) = Build(token: null);

        var result = await forge.DefaultBranch(_repo, TestContext.Current.CancellationToken);

        Assert.Equal(ForgeErrors.Unauthorized.Code, result.Error.Code);
        Assert.Empty(http.Seen);
    }

    [Fact]
    public async Task A_401_asks_the_credential_again_once_and_succeeds_with_the_new_token()
    {
        var http = new StubHandler().Sequence(HttpMethod.Get, "/repos/acme/widgets", (HttpStatusCode.Unauthorized, "{}"), (HttpStatusCode.OK, "{\"default_branch\":\"main\"}"));
        var (forge, _, credentials) = Build(http);

        var result = await forge.DefaultBranch(_repo, TestContext.Current.CancellationToken);

        Assert.Equal("main", result.Value);
        Assert.Equal([false, true], credentials.Refreshes);
        Assert.Equal(2, http.Seen.Count);
    }

    [Fact]
    public async Task A_second_401_is_AW_FORGE_003_after_exactly_one_refresh_and_never_names_the_token()
    {
        var http = new StubHandler().On(HttpMethod.Get, "/repos/acme/widgets", HttpStatusCode.Unauthorized, "{\"message\":\"Bad credentials\"}");
        var (forge, _, credentials) = Build(http);

        var result = await forge.DefaultBranch(_repo, TestContext.Current.CancellationToken);

        Assert.Equal(ForgeErrors.Unauthorized.Code, result.Error.Code);
        Assert.Equal([false, true], credentials.Refreshes);
        Assert.DoesNotContain(Secret, result.Error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task A_5xx_is_AW_FORGE_005(HttpStatusCode status)
    {
        var http = new StubHandler().On(HttpMethod.Get, "/repos/acme/widgets", status, "boom");
        var (forge, _, _) = Build(http);

        var result = await forge.DefaultBranch(_repo, TestContext.Current.CancellationToken);

        Assert.Equal(ForgeErrors.Unavailable.Code, result.Error.Code);
    }

    [Fact]
    public async Task A_transport_failure_is_AW_FORGE_005()
    {
        var http = new StubHandler().Break(HttpMethod.Get, "/repos/acme/widgets");
        var (forge, _, _) = Build(http);

        var result = await forge.DefaultBranch(_repo, TestContext.Current.CancellationToken);

        Assert.Equal(ForgeErrors.Unavailable.Code, result.Error.Code);
    }

    [Fact]
    public async Task A_429_is_AW_FORGE_006()
    {
        var http = new StubHandler().On(HttpMethod.Get, "/repos/acme/widgets", HttpStatusCode.TooManyRequests, "{}");
        var (forge, _, _) = Build(http);

        var result = await forge.DefaultBranch(_repo, TestContext.Current.CancellationToken);

        Assert.Equal(ForgeErrors.RateLimited.Code, result.Error.Code);
    }

    [Fact]
    public async Task A_403_with_no_rate_limit_left_is_AW_FORGE_006_and_without_it_is_AW_FORGE_003()
    {
        var limited = new StubHandler().On(HttpMethod.Get, "/repos/acme/widgets", HttpStatusCode.Forbidden, "{}", ("X-RateLimit-Remaining", "0"));
        var plain = new StubHandler().On(HttpMethod.Get, "/repos/acme/widgets", HttpStatusCode.Forbidden, "{\"message\":\"Resource not accessible\"}");

        var first = await Build(limited).Forge.DefaultBranch(_repo, TestContext.Current.CancellationToken);
        var second = await Build(plain).Forge.DefaultBranch(_repo, TestContext.Current.CancellationToken);

        Assert.Equal(ForgeErrors.RateLimited.Code, first.Error.Code);
        Assert.Equal(ForgeErrors.Unauthorized.Code, second.Error.Code);
    }

    [Fact]
    public async Task A_404_is_AW_FORGE_004_and_an_unexpected_4xx_is_AW_FORGE_007()
    {
        var missing = new StubHandler().On(HttpMethod.Get, "/repos/acme/widgets", HttpStatusCode.NotFound, "{\"message\":\"Not Found\"}");
        var refused = new StubHandler().On(HttpMethod.Get, "/repos/acme/widgets", HttpStatusCode.UnprocessableEntity, "{\"message\":\"Validation Failed\"}");

        var first = await Build(missing).Forge.DefaultBranch(_repo, TestContext.Current.CancellationToken);
        var second = await Build(refused).Forge.DefaultBranch(_repo, TestContext.Current.CancellationToken);

        Assert.Equal(ForgeErrors.NotFound.Code, first.Error.Code);
        Assert.Equal(ForgeErrors.Rejected.Code, second.Error.Code);
    }

    [Fact]
    public async Task GetPull_reads_the_pull_its_check_runs_and_the_latest_review_decision()
    {
        const string pull = """{"html_url":"https://github.com/acme/widgets/pull/7","title":"T","state":"open","draft":false,"merged":false,"mergeable":true,"user":{"login":"ada"},"base":{"ref":"main","sha":"b","repo":{"full_name":"acme/widgets","default_branch":"main"}},"head":{"ref":"feature","sha":"abc123","repo":{"full_name":"acme/widgets"}}}""";
        const string runs = """{"check_runs":[{"id":1,"name":"build","status":"completed","conclusion":"success"},{"id":2,"name":"lint","status":"completed","conclusion":"failure"}]}""";
        const string reviews = """[{"state":"APPROVED","user":{"login":"bob"}},{"state":"CHANGES_REQUESTED","user":{"login":"bob"}}]""";
        var http = new StubHandler()
            .On(HttpMethod.Get, "/repos/acme/widgets/pulls/7/reviews", HttpStatusCode.OK, reviews)
            .On(HttpMethod.Get, "/repos/acme/widgets/pulls/7", HttpStatusCode.OK, pull)
            .On(HttpMethod.Get, "/repos/acme/widgets/commits/abc123/check-runs", HttpStatusCode.OK, runs);
        var (forge, _, _) = Build(http);

        var result = await forge.GetPull(Pull(), TestContext.Current.CancellationToken);

        var snapshot = result.Value;
        Assert.Equal(PullStatus.Open, snapshot.Status);
        Assert.Equal("ada", snapshot.Author);
        Assert.Equal("feature", snapshot.Head.Ref);
        Assert.False(snapshot.Head.Fork);
        Assert.True(snapshot.Base.IsDefault);
        Assert.Equal(ChecksState.Failing, snapshot.Checks.State);
        Assert.Equal(["lint"], snapshot.Checks.Failing);
        Assert.Equal("CHANGES_REQUESTED", snapshot.ReviewDecision);
    }

    [Fact]
    public async Task A_review_list_is_followed_across_pages_on_the_same_origin_only()
    {
        const string link = "<https://api.github.com/repos/acme/widgets/pulls/7/reviews?per_page=100&page=2>; rel=\"next\"";
        var http = new StubHandler()
            .On(HttpMethod.Get, "/repos/acme/widgets/pulls/7/reviews?per_page=100&page=2", HttpStatusCode.OK, """[{"state":"APPROVED","user":{"login":"bob"}}]""")
            .On(HttpMethod.Get, "/repos/acme/widgets/pulls/7/reviews", HttpStatusCode.OK, "[]", ("Link", link))
            .On(HttpMethod.Get, "/repos/acme/widgets/pulls/7", HttpStatusCode.OK, """{"state":"open","head":{"ref":"f","sha":"abc"},"base":{"ref":"main"}}""")
            .On(HttpMethod.Get, "/repos/acme/widgets/commits/abc/check-runs", HttpStatusCode.OK, """{"check_runs":[]}""");
        var (forge, _, _) = Build(http);

        var result = await forge.GetPull(Pull(), TestContext.Current.CancellationToken);

        Assert.Equal("APPROVED", result.Value.ReviewDecision);
        Assert.Equal(ChecksState.None, result.Value.Checks.State);
    }

    [Fact]
    public async Task A_merge_maps_200_to_Merged_409_to_HeadMoved_and_405_to_Blocked_with_the_reason()
    {
        var request = new MergeRequest(Pull(), MergeMethod.Squash, "abc123");
        var merged = new StubHandler().On(HttpMethod.Put, "/repos/acme/widgets/pulls/7/merge", HttpStatusCode.OK, """{"sha":"def456","merged":true}""");
        var moved = new StubHandler().On(HttpMethod.Put, "/repos/acme/widgets/pulls/7/merge", HttpStatusCode.Conflict, """{"message":"Head branch was modified"}""");
        var blocked = new StubHandler().On(HttpMethod.Put, "/repos/acme/widgets/pulls/7/merge", HttpStatusCode.MethodNotAllowed, """{"message":"Required status check is expected"}""");

        var first = (await Build(merged).Forge.Merge(request, TestContext.Current.CancellationToken)).Value;
        var second = (await Build(moved).Forge.Merge(request, TestContext.Current.CancellationToken)).Value;
        var third = (await Build(blocked).Forge.Merge(request, TestContext.Current.CancellationToken)).Value;

        Assert.Equal((MergeStatus.Merged, "def456"), (first.Status, first.MergedSha));
        Assert.Equal(MergeStatus.HeadMoved, second.Status);
        Assert.Equal(MergeStatus.Blocked, third.Status);
        Assert.Equal("Required status check is expected", third.Reason);
    }

    [Fact]
    public async Task A_merge_sends_the_expected_head_as_the_sha_precondition()
    {
        var handler = new RecordingBodyHandler();
        var credentials = new CountingCredentials(Secret);
        var forge = new GitHubForge(new GitHubApi(new HttpClient(handler), credentials, new ForgeHosts(["github.com"])), credentials, new GitPusher(Redactor.Default), Redactor.Default);

        _ = await forge.Merge(new MergeRequest(Pull(), MergeMethod.Rebase, "abc123"), TestContext.Current.CancellationToken);

        Assert.Contains("\"sha\":\"abc123\"", handler.Body, StringComparison.Ordinal);
        Assert.Contains("\"merge_method\":\"rebase\"", handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckLog_returns_the_scrubbed_tail_of_the_named_check_and_AW_FORGE_004_for_an_unknown_one()
    {
        var output = new string('x', ForgeLimits.LogExcerptCharacters + 500) + " tail-line";
        var runs = "{\"check_runs\":[{\"id\":1,\"name\":\"build\",\"status\":\"completed\",\"conclusion\":\"failure\",\"output\":{\"title\":\"\",\"summary\":\"\",\"text\":\"" + output + "\"}}]}";
        var http = new StubHandler()
            .On(HttpMethod.Get, "/repos/acme/widgets/pulls/7", HttpStatusCode.OK, """{"state":"open","head":{"ref":"f","sha":"abc"},"base":{"ref":"main"}}""")
            .On(HttpMethod.Get, "/repos/acme/widgets/commits/abc/check-runs", HttpStatusCode.OK, runs);
        var (forge, _, _) = Build(http);

        var log = await forge.CheckLog(Pull(), "build", TestContext.Current.CancellationToken);
        var unknown = await forge.CheckLog(Pull(), "nope", TestContext.Current.CancellationToken);

        Assert.True(log.Value.Length <= ForgeLimits.LogExcerptCharacters);
        Assert.EndsWith("tail-line", log.Value, StringComparison.Ordinal);
        Assert.Equal(ForgeErrors.NotFound.Code, unknown.Error.Code);
    }

    [Fact]
    public async Task Push_refuses_a_remote_that_is_not_https_or_carries_credentials_and_one_outside_the_hosts()
    {
        var (forge, http, credentials) = Build();
        var cancel = TestContext.Current.CancellationToken;

        var plain = await forge.Push(new PushRequest(RepoDir: "/x", Sha: new string('a', 40), Remote: "http://github.com/acme/widgets.git", Branch: "main", LeaseSha: null), cancel);
        var withUser = await forge.Push(new PushRequest(RepoDir: "/x", Sha: new string('a', 40), Remote: "https://user:pw@github.com/acme/widgets.git", Branch: "main", LeaseSha: null), cancel);
        var elsewhere = await forge.Push(new PushRequest(RepoDir: "/x", Sha: new string('a', 40), Remote: "https://evil.example/acme/widgets.git", Branch: "main", LeaseSha: null), cancel);

        Assert.Equal(ForgeErrors.Invalid.Code, plain.Error.Code);
        Assert.Equal(ForgeErrors.Invalid.Code, withUser.Error.Code);
        Assert.Equal(ForgeErrors.HostNotAllowed.Code, elsewhere.Error.Code);
        Assert.Empty(http.Seen);
        Assert.Empty(credentials.Refreshes);
    }

    [Fact]
    public void Parse_uses_the_github_kind_and_the_allowed_hosts()
    {
        var (forge, _, _) = Build();

        var ok = forge.Parse("https://github.com/acme/widgets/pull/7");
        var refused = forge.Parse("https://gitlab.com/acme/widgets/pull/7");

        Assert.Equal(ForgeKind.GitHub, ok.Value.Forge);
        Assert.Equal(ForgeErrors.HostNotAllowed.Code, refused.Error.Code);
    }

    private sealed class CountingCredentials(string? token) : ICredentialSource
    {
        internal List<bool> Refreshes { get; } = [];

        public ValueTask<Result<string>> Token(string host, bool refresh, CancellationToken cancellationToken)
        {
            Refreshes.Add(refresh);
            return ValueTask.FromResult(token is null
                ? Results.Failure<string>(ForgeErrors.Unauthorized.With("no token"))
                : Results.Success(token));
        }
    }

    private sealed class RecordingBodyHandler : HttpMessageHandler
    {
        internal string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"sha\":\"s\"}") };
        }
    }
}
