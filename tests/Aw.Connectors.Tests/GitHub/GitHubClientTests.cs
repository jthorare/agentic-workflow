using System.Net;
using System.Net.Sockets;
using Aw.Connectors.Core;
using Aw.Connectors.Core.GitHub;
using Aw.Connectors.Infrastructure.GitHub;
using Aw.Connectors.Testing;
using Aw.Connectors.Testing.GitHub;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Connectors.Tests.GitHub;

public sealed class GitHubClientTests : IDisposable
{
    private const string Repo = "acme/widgets";
    private const string Token = "tok_unit_test_value_42";
    private static DateTimeOffset T => new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static IPAddress PublicAddress => IPAddress.Parse("140.82.112.5");

    private readonly FakeGitHubServer _server = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 27, 9, 0, 0, TimeSpan.Zero));
    private readonly List<string> _resolved = [];
    private Func<string, IReadOnlyList<IPAddress>> _dns = _ => [PublicAddress];

    public void Dispose() => _server.Dispose();

    private static GitHubCursor Start => GitHubCursor.From(T);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private GitHubClient Client(string? host = null, bool allowPrivate = false, string? token = Token)
        => new(new HttpClient(_server), new GitHubClientSettings { Host = host, AllowPrivateHost = allowPrivate, Token = token }, new FakeResolver(this), _clock);

    [Fact, Trait("Lane", "unit")]
    public async Task The_first_request_asks_for_the_list_oldest_change_first_with_the_token_as_a_bearer()
    {
        _ = _server.Add(Repo, A.GitHubIssue(1).UpdatedAt(T.AddMinutes(1)));

        var result = await Client().Issues(Repo, Start, 100, before: null, Ct);

        Assert.True(result.IsSuccess);
        var request = Assert.Single(_server.Requests);
        Assert.Equal(
            "https://api.github.com/repos/acme/widgets/issues?state=all&sort=updated&direction=asc&since=2026-10-01T12:00:00Z&per_page=100",
            Uri.UnescapeDataString(request.Uri.AbsoluteUri));
        Assert.Equal($"Bearer {Token}", request.Authorization);
        Assert.NotEmpty(request.UserAgent);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_repo_wide_comment_read_asks_for_updated_ascending_from_since_and_stops_after_the_page_past_until()
    {
        for (var i = 1; i <= 5; i++)
        {
            _ = _server.AddComment(Repo, A.GitHubComment(i).On(7).UpdatedAt(T.AddHours(i)));
        }

        _server.MaxPageSize = 2;

        var page = (await Client().Comments(Repo, T, T.AddHours(3), Ct)).Value;

        Assert.Equal([1, 2, 3], page.Items.Select(static c => c.Id));
        Assert.Equal(2, _server.Requests.Count);
        Assert.Equal(
            "https://api.github.com/repos/acme/widgets/issues/comments?sort=updated&direction=asc&since=2026-10-01T12:00:00Z&per_page=100",
            Uri.UnescapeDataString(_server.Requests[0].Uri.AbsoluteUri.Replace("&page=1", string.Empty, StringComparison.Ordinal)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rate_limit_in_the_comment_read_comes_back_as_a_value_with_what_was_read()
    {
        var reset = new DateTimeOffset(2026, 10, 27, 10, 0, 0, TimeSpan.Zero);
        for (var i = 1; i <= 3; i++)
        {
            _ = _server.AddComment(Repo, A.GitHubComment(i).UpdatedAt(T.AddHours(i)));
        }

        _server.MaxPageSize = 2;
        _ = _server.ScriptThrough().ScriptRateLimit(reset);

        var page = (await Client().Comments(Repo, T, T.AddDays(1), Ct)).Value;

        Assert.Equal(reset, page.RateLimitedUntil);
        Assert.Equal([1, 2], page.Items.Select(static c => c.Id));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_request_is_read_from_its_own_route_and_its_files_route()
    {
        _ = _server.AddPull(Repo, 12, new GitHubPullDetail(Merged: true, Draft: false, T, ["a.cs"]));

        var read = (await Client().PullRequest(Repo, 12, Ct)).Value;

        Assert.True(read.Detail!.Merged);
        Assert.Equal(
            ["/repos/acme/widgets/pulls/12", "/repos/acme/widgets/pulls/12/files"],
            _server.Requests.Select(static r => r.Uri.AbsolutePath),
            StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rate_limit_on_the_files_route_means_no_detail()
    {
        var reset = new DateTimeOffset(2026, 10, 27, 10, 0, 0, TimeSpan.Zero);
        _ = _server.AddPull(Repo, 12, new GitHubPullDetail(Merged: true, Draft: false, T, ["a.cs"])).ScriptThrough().ScriptRateLimit(reset);

        var read = (await Client().PullRequest(Repo, 12, Ct)).Value;

        Assert.Null(read.Detail);
        Assert.Equal(reset, read.RateLimitedUntil);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_request_the_server_does_not_have_is_not_found()
    {
        var result = await Client().PullRequest(Repo, 99, Ct);

        Assert.Equal(ConnectorsErrors.NotFound.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_token_no_authorization_is_sent()
    {
        _ = _server.Add(Repo, A.GitHubIssue(1).UpdatedAt(T));

        _ = await Client(token: null).Issues(Repo, Start, 100, before: null, Ct);

        Assert.Null(Assert.Single(_server.Requests).Authorization);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pages_are_followed_by_the_link_header_and_pull_requests_are_marked()
    {
        _server.MaxPageSize = 2;
        for (var n = 1; n <= 5; n++)
        {
            var builder = A.GitHubIssue(n).UpdatedAt(T.AddMinutes(n));
            _ = _server.Add(Repo, n == 3 ? builder.AsPullRequest() : builder);
        }

        var page = (await Client().Issues(Repo, Start, 1000, before: null, Ct)).Value;

        Assert.Equal([1, 2, 3, 4, 5], page.Items.Select(i => i.Id));
        Assert.Equal([false, false, true, false, false], page.Items.Select(i => i.IsPullRequest));
        Assert.Equal(3, _server.Requests.Count);
        Assert.Null(page.RateLimitedUntil);
        Assert.Equal(T.AddMinutes(5), page.Next.UpdatedAt);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_fields_of_an_entry_are_read()
    {
        _ = _server.Add(Repo, A.GitHubIssue(7).UpdatedAt(T.AddHours(1)).WithBody("Crash on start.").Closed());

        var issue = Assert.Single((await Client().Issues(Repo, Start, 100, before: null, Ct)).Value.Items);

        Assert.Equal(7, issue.Number);
        Assert.Equal("Issue 7", issue.Title);
        Assert.Equal("Crash on start.", issue.Body);
        Assert.Equal("closed", issue.State);
        Assert.Equal("octocat", issue.Author);
        Assert.Equal("https://github.com/acme/widgets/issues/7", issue.Url);
        Assert.Equal(T.AddHours(1), issue.UpdatedAt);
        Assert.Equal(T.AddHours(1), issue.ClosedAt);
        Assert.Equal(["bug"], issue.Labels);
        Assert.Equal(0, issue.Comments);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_entries_a_cursor_has_seen_at_its_instant_are_skipped_and_the_others_at_that_instant_are_kept()
    {
        _ = _server.Add(Repo, A.GitHubIssue(1).UpdatedAt(T))
            .Add(Repo, A.GitHubIssue(2).UpdatedAt(T))
            .Add(Repo, A.GitHubIssue(3).UpdatedAt(T.AddSeconds(1)));

        var page = (await Client().Issues(Repo, new GitHubCursor(T, [1]), 100, before: null, Ct)).Value;

        Assert.Equal([2, 3], page.Items.Select(i => i.Id));
        Assert.Equal(T.AddSeconds(1), page.Next.UpdatedAt);
        Assert.Equal<long>([3], page.Next.SeenIds);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_run_cut_inside_one_instant_loses_nothing_and_repeats_nothing()
    {
        _server.MaxPageSize = 2;
        _ = _server.Add(Repo, A.GitHubIssue(1).UpdatedAt(T))
            .Add(Repo, A.GitHubIssue(2).UpdatedAt(T))
            .Add(Repo, A.GitHubIssue(3).UpdatedAt(T));

        var first = (await Client().Issues(Repo, GitHubCursor.From(T), 2, before: null, Ct)).Value;
        var second = (await Client().Issues(Repo, first.Next, 2, before: null, Ct)).Value;
        var third = (await Client().Issues(Repo, second.Next, 2, before: null, Ct)).Value;

        Assert.Equal([1, 2], first.Items.Select(i => i.Id));
        Assert.Equal([3], second.Items.Select(i => i.Id));
        Assert.Empty(third.Items);
        Assert.Equal<long>([1, 2, 3], third.Next.SeenIds);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_bound_stops_paging_at_the_first_entry_at_or_after_it()
    {
        _server.MaxPageSize = 2;
        _ = _server.Add(Repo, A.GitHubIssue(1).UpdatedAt(T.AddMinutes(1)))
            .Add(Repo, A.GitHubIssue(2).UpdatedAt(T.AddMinutes(5)))
            .Add(Repo, A.GitHubIssue(3).UpdatedAt(T.AddMinutes(9)))
            .Add(Repo, A.GitHubIssue(4).UpdatedAt(T.AddMinutes(12)));

        var page = (await Client().Issues(Repo, GitHubCursor.From(T), 1000, T.AddMinutes(5), Ct)).Value;

        Assert.Equal([1], page.Items.Select(static i => i.Id));
        _ = Assert.Single(_server.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Replaying_the_returned_cursor_returns_nothing_and_keeps_the_cursor()
    {
        _ = _server.Add(Repo, A.GitHubIssue(1).UpdatedAt(T.AddMinutes(1))).Add(Repo, A.GitHubIssue(2).UpdatedAt(T.AddMinutes(1)));
        var first = (await Client().Issues(Repo, Start, 100, before: null, Ct)).Value;

        var replay = (await Client().Issues(Repo, first.Next, 100, before: null, Ct)).Value;

        Assert.Empty(replay.Items);
        Assert.Equal(first.Next.UpdatedAt, replay.Next.UpdatedAt);
        Assert.Equal(first.Next.SeenIds, replay.Next.SeenIds);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rate_limit_after_a_page_returns_what_was_read_with_the_reset_time_and_no_sleep()
    {
        _server.MaxPageSize = 2;
        for (var n = 1; n <= 4; n++)
        {
            _ = _server.Add(Repo, A.GitHubIssue(n).UpdatedAt(T.AddMinutes(n)));
        }

        var reset = _clock.GetUtcNow().AddMinutes(37);
        _ = _server.ScriptThrough().ScriptRateLimit(reset);

        var result = await Client().Issues(Repo, Start, 1000, before: null, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal([1, 2], result.Value.Items.Select(i => i.Id));
        Assert.Equal(reset, result.Value.RateLimitedUntil);
        Assert.Equal(T.AddMinutes(2), result.Value.Next.UpdatedAt);
        Assert.Equal<long>([2], result.Value.Next.SeenIds);
        Assert.Equal(2, _server.Requests.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rate_limit_on_the_first_request_returns_no_items_and_the_same_cursor()
    {
        var reset = _clock.GetUtcNow().AddMinutes(10);
        _ = _server.ScriptRateLimit(reset);
        var cursor = new GitHubCursor(T, [5]);

        var page = (await Client().Issues(Repo, cursor, 100, before: null, Ct)).Value;

        Assert.Empty(page.Items);
        Assert.Equal(reset, page.RateLimitedUntil);
        Assert.Equal(T, page.Next.UpdatedAt);
        Assert.Equal<long>([5], page.Next.SeenIds);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task A_retry_after_counts_from_now(HttpStatusCode status)
    {
        _ = _server.ScriptRetryAfter(status, 90);

        var page = (await Client().Issues(Repo, Start, 100, before: null, Ct)).Value;

        Assert.Equal(_clock.GetUtcNow().AddSeconds(90), page.RateLimitedUntil);
        Assert.Empty(page.Items);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_429_without_headers_still_stops_the_run_with_a_default_wait()
    {
        _ = _server.ScriptStatus(HttpStatusCode.TooManyRequests);

        var page = (await Client().Issues(Repo, Start, 100, before: null, Ct)).Value;

        Assert.Equal(_clock.GetUtcNow().AddSeconds(60), page.RateLimitedUntil);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_403_that_is_not_a_rate_limit_is_access_denied()
    {
        _ = _server.ScriptStatus(HttpStatusCode.Forbidden);

        var result = await Client().Issues(Repo, Start, 100, before: null, Ct);

        Assert.Equal(ConnectorsErrors.AccessDenied.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_401_is_a_typed_error()
    {
        _ = _server.ScriptStatus(HttpStatusCode.Unauthorized);

        var result = await Client().Issues(Repo, Start, 100, before: null, Ct);

        Assert.Equal(ConnectorsErrors.Unauthorized.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_404_says_the_repo_is_missing_or_not_visible()
    {
        _ = _server.ScriptStatus(HttpStatusCode.NotFound);

        var result = await Client().Issues(Repo, Start, 100, before: null, Ct);

        Assert.Equal(ConnectorsErrors.NotFound.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_server_error_is_a_bad_reply()
    {
        _ = _server.ScriptStatus(HttpStatusCode.BadGateway);

        var result = await Client().Issues(Repo, Start, 100, before: null, Ct);

        Assert.Equal(ConnectorsErrors.BadReply.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_body_that_is_not_the_list_is_a_bad_reply()
    {
        _ = _server.Script(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"not\":\"a list\"}") });

        var result = await Client().Issues(Repo, Start, 100, before: null, Ct);

        Assert.Equal(ConnectorsErrors.BadReply.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_redirect_is_refused_and_not_followed()
    {
        _ = _server.ScriptRedirect("https://evil.example.com/steal");

        var result = await Client().Issues(Repo, Start, 100, before: null, Ct);

        Assert.Equal(ConnectorsErrors.RedirectRefused.Code, result.Error.Code);
        _ = Assert.Single(_server.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_next_link_that_leaves_the_api_host_is_refused_without_a_request_to_it()
    {
        _ = _server.Script(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") };
            response.Headers.Add("Link", "<https://evil.example.com/repos/acme/widgets/issues?page=2>; rel=\"next\"");
            return response;
        });

        var result = await Client().Issues(Repo, Start, 100, before: null, Ct);

        Assert.Equal(ConnectorsErrors.BadReply.Code, result.Error.Code);
        _ = Assert.Single(_server.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_enterprise_host_maps_to_its_api_v3_root()
    {
        _ = _server.Add(Repo, A.GitHubIssue(1).UpdatedAt(T));

        var result = await Client(host: "ghe.example.com:8443").Issues(Repo, Start, 100, before: null, Ct);

        Assert.True(result.IsSuccess);
        Assert.StartsWith("https://ghe.example.com:8443/api/v3/repos/acme/widgets/issues?", Assert.Single(_server.Requests).Uri.AbsoluteUri, StringComparison.Ordinal);
        Assert.Equal(["ghe.example.com"], _resolved);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("ghe.example.com/evil")]
    [InlineData("https://ghe.example.com")]
    [InlineData("user@ghe.example.com")]
    [InlineData("ghe.example.com?x=1")]
    [InlineData("ghe example.com")]
    public async Task A_host_that_is_not_a_bare_host_name_is_invalid(string host)
    {
        var result = await Client(host: host).Issues(Repo, Start, 100, before: null, Ct);

        Assert.Equal(ConnectorsErrors.Invalid.Code, result.Error.Code);
        Assert.Empty(_server.Requests);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("acme")]
    [InlineData("acme/widgets/extra")]
    [InlineData("acme/wid gets")]
    [InlineData("../widgets")]
    [InlineData("")]
    public async Task A_repo_that_is_not_owner_slash_name_is_invalid(string repo)
    {
        var result = await Client().Issues(repo, Start, 100, before: null, Ct);

        Assert.Equal(ConnectorsErrors.Invalid.Code, result.Error.Code);
        Assert.Empty(_server.Requests);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("10.0.0.5")]
    [InlineData("127.0.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    public async Task A_host_that_resolves_to_a_private_address_is_refused_before_any_request(string address)
    {
        _dns = _ => [IPAddress.Parse(address)];

        var result = await Client(host: "ghe.internal.example").Issues(Repo, Start, 100, before: null, Ct);

        Assert.Equal(ConnectorsErrors.HostRefused.Code, result.Error.Code);
        Assert.Empty(_server.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_host_with_one_private_address_among_public_ones_is_refused()
    {
        _dns = _ => [PublicAddress, IPAddress.Parse("10.0.0.5")];

        var result = await Client(host: "ghe.example.com").Issues(Repo, Start, 100, before: null, Ct);

        Assert.Equal(ConnectorsErrors.HostRefused.Code, result.Error.Code);
        Assert.Empty(_server.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_allow_flag_lets_a_private_host_through()
    {
        _dns = _ => [IPAddress.Parse("10.0.0.5")];
        _ = _server.Add(Repo, A.GitHubIssue(1).UpdatedAt(T));

        var result = await Client(host: "ghe.internal.example", allowPrivate: true).Issues(Repo, Start, 100, before: null, Ct);

        Assert.True(result.IsSuccess);
        _ = Assert.Single(_server.Requests);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("203.0.113.9")]
    [InlineData("10.0.0.5")]
    [InlineData("[::1]")]
    public async Task An_ip_literal_host_is_refused_unless_the_profile_allows_it(string host)
    {
        var refused = await Client(host: host).Issues(Repo, Start, 100, before: null, Ct);
        _ = _server.Add(Repo, A.GitHubIssue(1).UpdatedAt(T));
        var allowed = await Client(host: host, allowPrivate: true).Issues(Repo, Start, 100, before: null, Ct);

        Assert.Equal(ConnectorsErrors.HostRefused.Code, refused.Error.Code);
        Assert.True(allowed.IsSuccess);
        Assert.Empty(_resolved);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_host_that_does_not_resolve_is_unreachable()
    {
        _dns = _ => throw new SocketException((int)SocketError.HostNotFound);

        var result = await Client(host: "nope.example.com").Issues(Repo, Start, 100, before: null, Ct);

        Assert.Equal(ConnectorsErrors.Unreachable.Code, result.Error.Code);
        Assert.Empty(_server.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_token_with_whitespace_is_invalid_and_not_echoed()
    {
        const string bad = "tok_bad token\nvalue";

        var result = await Client(token: bad).Issues(Repo, Start, 100, before: null, Ct);

        Assert.Equal(ConnectorsErrors.Invalid.Code, result.Error.Code);
        Assert.DoesNotContain("tok_bad", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_server.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_token_is_in_no_error_message()
    {
        var errors = new List<string>();

        async Task Collect(Action arrange, string? host = null)
        {
            arrange();
            var result = await Client(host: host).Issues(Repo, Start, 100, before: null, Ct);
            if (result.IsError)
            {
                errors.Add(result.Error.Message);
            }
        }

        await Collect(() => _server.ScriptStatus(HttpStatusCode.Unauthorized));
        await Collect(() => _server.ScriptStatus(HttpStatusCode.Forbidden));
        await Collect(() => _server.ScriptStatus(HttpStatusCode.NotFound));
        await Collect(() => _server.ScriptStatus(HttpStatusCode.InternalServerError));
        await Collect(() => _server.ScriptRedirect("https://evil.example.com/?t=" + Token));
        await Collect(() => _server.Script(_ => throw new HttpRequestException("connection reset " + Token)));
        await Collect(() => _server.Script(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Bearer " + Token) }));
        _dns = _ => [IPAddress.Parse("10.0.0.5")];
        await Collect(() => { }, host: "ghe.internal.example");
        _dns = _ => throw new SocketException((int)SocketError.HostNotFound);
        await Collect(() => { }, host: "nope.example.com");

        Assert.Equal(9, errors.Count);
        Assert.All(errors, message => Assert.DoesNotContain(Token, message, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_settings_withhold_the_token_when_printed()
    {
        var text = new GitHubClientSettings { Host = "ghe.example.com", Token = Token }.ToString();

        Assert.DoesNotContain(Token, text, StringComparison.Ordinal);
        Assert.Contains("ghe.example.com", text, StringComparison.Ordinal);
    }

    private sealed class FakeResolver(GitHubClientTests owner) : IHostResolver
    {
        public ValueTask<IReadOnlyList<IPAddress>> Resolve(string host, CancellationToken cancellationToken)
        {
            owner._resolved.Add(host);
            return ValueTask.FromResult(owner._dns(host));
        }
    }
}
