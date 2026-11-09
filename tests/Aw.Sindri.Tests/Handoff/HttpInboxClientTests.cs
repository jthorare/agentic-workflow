using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Handoff;
using Aw.Sindri.Infrastructure.Handoff;

namespace Aw.Sindri.Tests.Handoff;

/// <summary>What only the HTTP client does: retries, the allowlist, the 30 s limit and the strictness about the inbox's answers.</summary>
[Lane(TestLane.Integration)]
public sealed class HttpInboxClientTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Offer Sample(string key) => new(key, "Title", "acme/web", Branch: null, Prompt: null, Links: [], Labels: []);

    [Fact]
    public async Task List_is_retried_after_a_503_and_then_succeeds()
    {
        using var inbox = InboxPairs.Http(out var server);
        inbox.Backing.Seed("me", Sample("o-1"));
        server.FailNext("GET /offers", 503, 2);

        var listed = await inbox.Client.ListOffers(inbox.Target("me"), pageToken: null, Token);

        Assert.False(listed.IsError);
        Assert.Equal(3, server.Requests.Count(static r => r.StartsWith("GET /offers", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task List_gives_up_after_three_tries_as_SND_CONN_205()
    {
        using var inbox = InboxPairs.Http(out var server);
        server.FailNext("GET /offers", 503, 10);

        var listed = await inbox.Client.ListOffers(inbox.Target("me"), pageToken: null, Token);

        Assert.Equal(SindriErrors.InboxUnavailable.Code, listed.Error.Code);
        Assert.Equal(3, server.Requests.Count);
    }

    [Fact]
    public async Task Submit_is_retried_after_a_503()
    {
        using var inbox = InboxPairs.Http(out var server);
        inbox.Backing.Seed("me", Sample("o-1"));
        var claim = (await inbox.Client.Claim(inbox.Target("me"), "o-1", Token)).Value;
        server.FailNext("POST /offers/o-1/result", 503, 1);

        var submitted = await inbox.Client.Submit(inbox.Target("me"), "o-1", new InboxResult(claim.ClaimKey, Succeeded: true, "ok", []), Token);

        Assert.False(submitted.IsError);
        Assert.Equal(2, server.Requests.Count(static r => r.StartsWith("POST /offers/o-1/result", StringComparison.Ordinal)));
        Assert.Equal(claim.ClaimKey, Assert.Single(inbox.Backing.Submissions).Result.ClaimKey);
    }

    [Fact]
    public async Task Claim_is_never_retried()
    {
        using var inbox = InboxPairs.Http(out var server);
        inbox.Backing.Seed("me", Sample("o-1"));
        server.FailNext("POST /offers/o-1/claim", 503, 1);

        var claimed = await inbox.Client.Claim(inbox.Target("me"), "o-1", Token);

        Assert.True(claimed.IsError);
        Assert.Equal(SindriErrors.InboxUnavailable.Code, claimed.Error.Code);
        Assert.Equal(1, server.Requests.Count(static r => r.StartsWith("POST /offers/o-1/claim", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_host_off_the_allowlist_is_SND_CONN_206_and_nothing_is_sent()
    {
        using var inbox = InboxPairs.Http(out var server);
        var client = new HttpInboxClient(new HttpClient(), ["inbox.example.test"], TimeSpan.Zero);

        var listed = await client.ListOffers(inbox.Target("me"), pageToken: null, Token);

        Assert.Equal(SindriErrors.InboxHostNotAllowed.Code, listed.Error.Code);
        Assert.Empty(server.Requests);
    }

    [Fact]
    public async Task Plain_http_is_refused_for_a_host_that_is_not_loopback()
    {
        var client = new HttpInboxClient(new HttpClient(), ["inbox.example.test"], TimeSpan.Zero);
        var target = new InboxTarget(new Uri("http://inbox.example.test/"), new TokenSet("k", RefreshToken: null, ExpiresAt: null), "me");

        var listed = await client.ListOffers(target, pageToken: null, Token);

        Assert.Equal(SindriErrors.InboxHostNotAllowed.Code, listed.Error.Code);
    }

    [Fact]
    public async Task An_offer_without_a_title_is_SND_CONN_201_naming_the_field()
    {
        using var inbox = InboxPairs.Http(out var server);
        server.ReplyNext("GET /offers", "{\"offers\":[{\"id\":\"o-1\",\"repo\":\"acme/web\",\"links\":[],\"labels\":[]}],\"nextPageToken\":null}");

        var listed = await inbox.Client.ListOffers(inbox.Target("me"), pageToken: null, Token);

        Assert.Equal(SindriErrors.InboxContractViolation.Code, listed.Error.Code);
        Assert.Contains("title", listed.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_body_that_is_not_json_is_SND_CONN_201()
    {
        using var inbox = InboxPairs.Http(out var server);
        server.ReplyNext("GET /offers", "<html>maintenance</html>");

        var listed = await inbox.Client.ListOffers(inbox.Target("me"), pageToken: null, Token);

        Assert.Equal(SindriErrors.InboxContractViolation.Code, listed.Error.Code);
    }

    [Fact]
    public async Task The_error_text_holds_no_key()
    {
        using var inbox = InboxPairs.Http();

        var listed = await inbox.Client.ListOffers(inbox.WrongKey("me"), pageToken: null, Token);

        Assert.DoesNotContain("not-" + InboxPairs.Key, listed.Error.Message, StringComparison.Ordinal);
    }
}
