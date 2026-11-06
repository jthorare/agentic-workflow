using System.Net;
using System.Text;
using Aw.Forge.Core;
using Aw.Forge.Core.Pulls;
using Aw.Forge.Core.Refs;
using Aw.Forge.Infrastructure.Credentials;
using Aw.Forge.Infrastructure.Git;
using Aw.Forge.Infrastructure.GitHub;
using Aw.Kernel;
using Aw.Redaction;

namespace Aw.Forge.Tests.GitHub;

/// <summary>GitHubForge's review-page members over a scripted HTTP handler: files, file text, check runs and statuses, and the review post.</summary>
[Trait("Lane", "unit")]
public sealed class GitHubForgeReviewTests
{
    private const string Head = "abc123";
    private const string PullJson = """{"html_url":"https://github.com/acme/widgets/pull/7","title":"T","state":"open","draft":false,"merged":false,"mergeable":true,"user":{"login":"ada"},"base":{"ref":"main","sha":"b","repo":{"full_name":"acme/widgets","default_branch":"main"}},"head":{"ref":"feature","sha":"abc123","repo":{"full_name":"acme/widgets"}}}""";
    private static readonly ForgeRef _pull = new(ForgeKind.GitHub, "github.com", "acme", "widgets", 7);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static GitHubForge Build(StubHandler http)
    {
        var credentials = new FixedToken();
        return new GitHubForge(new GitHubApi(new HttpClient(http), credentials, new ForgeHosts(["github.com"])), credentials, new GitPusher(Redactor.Default), Redactor.Default);
    }

    private static string FilesPage(int from, int count) =>
        "[" + string.Join(',', Enumerable.Range(from, count).Select(i => Inv.S($$"""{"filename":"src/f{{i}}.cs","status":"modified","additions":1,"deletions":0,"patch":"@@ -1 +1 @@"}"""))) + "]";

    private static (string Name, string Value) Next(string path, int page) =>
        ("Link", Inv.S($"<https://api.github.com{path}?per_page=100&page={page}>; rel=\"next\""));

    [Fact]
    public async Task Files_reads_pages_of_100_and_follows_the_next_link()
    {
        var http = new StubHandler()
            .On(HttpMethod.Get, "/repos/acme/widgets/pulls/7/files?per_page=100&page=2", HttpStatusCode.OK, FilesPage(100, 20))
            .On(HttpMethod.Get, "/repos/acme/widgets/pulls/7/files", HttpStatusCode.OK, FilesPage(0, 100), Next("/repos/acme/widgets/pulls/7/files", 2));

        var files = (await Build(http).Files(_pull, Ct)).Value;

        Assert.Equal(120, files.Files.Count);
        Assert.False(files.CapReached);
        Assert.Equal("src/f119.cs", files.Files[^1].Path);
        Assert.Equal(2, http.Seen.Count);
        Assert.Contains("per_page=100", http.Seen[0].Url.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Files_stops_at_3000_and_says_the_cap_was_hit()
    {
        var http = new StubHandler().On(HttpMethod.Get, "/repos/acme/widgets/pulls/7/files", HttpStatusCode.OK, FilesPage(0, 100), Next("/repos/acme/widgets/pulls/7/files", 2));

        var files = (await Build(http).Files(_pull, Ct)).Value;

        Assert.Equal(ForgeLimits.PullFileCap, files.Files.Count);
        Assert.True(files.CapReached);
        Assert.Equal(30, http.Seen.Count);
    }

    [Fact]
    public async Task Files_maps_a_rename_a_binary_file_and_a_removal()
    {
        const string page = """[{"filename":"src/b.cs","previous_filename":"src/old-b.cs","status":"renamed","additions":2,"deletions":1,"patch":"@@"},{"filename":"logo.png","status":"added","additions":0,"deletions":0},{"filename":"gone.cs","status":"removed","additions":0,"deletions":9,"patch":"@@"}]""";
        var http = new StubHandler().On(HttpMethod.Get, "/repos/acme/widgets/pulls/7/files", HttpStatusCode.OK, page);

        var files = (await Build(http).Files(_pull, Ct)).Value.Files;

        Assert.Equal(new PullFile("src/b.cs", "src/old-b.cs", PullFileStatus.Renamed, 2, 1, "@@"), files[0]);
        Assert.Equal(new PullFile("logo.png", PreviousPath: null, PullFileStatus.Added, 0, 0, Patch: null), files[1]);
        Assert.Equal(PullFileStatus.Removed, files[2].Status);
    }

    [Fact]
    public async Task FileText_reads_the_contents_at_the_ref_and_decodes_base64()
    {
        var content = Convert.ToBase64String(Encoding.UTF8.GetBytes("class A;\n"), Base64FormattingOptions.InsertLineBreaks);
        var http = new StubHandler().On(HttpMethod.Get, "/repos/acme/widgets/contents/src/a%20b.cs?ref=abc123", HttpStatusCode.OK, $$"""{"type":"file","encoding":"base64","content":"{{content.ReplaceLineEndings("\\n")}}"}""");

        var text = await Build(http).FileText(_pull, "src/a b.cs", Head, Ct);

        Assert.Equal("class A;\n", text.Value);
        Assert.Equal("/repos/acme/widgets/contents/src/a%20b.cs?ref=abc123", Assert.Single(http.Seen).Url.PathAndQuery);
    }

    [Fact]
    public async Task FileText_is_capped_at_1_MiB()
    {
        var content = Convert.ToBase64String(Encoding.UTF8.GetBytes(new string('x', ForgeLimits.FileTextBytes + 100)));
        var http = new StubHandler().On(HttpMethod.Get, "/repos/acme/widgets/contents/big.txt", HttpStatusCode.OK, $$"""{"type":"file","encoding":"base64","content":"{{content}}"}""");

        var text = (await Build(http).FileText(_pull, "big.txt", Head, Ct)).Value;

        Assert.Equal(ForgeLimits.FileTextBytes, Encoding.UTF8.GetByteCount(text));
    }

    [Fact]
    public async Task FileText_of_a_directory_or_a_missing_file_is_not_found()
    {
        var http = new StubHandler().On(HttpMethod.Get, "/repos/acme/widgets/contents/src?ref=abc123", HttpStatusCode.OK, "[]");
        var forge = Build(http);

        var directory = await forge.FileText(_pull, "src", Head, Ct);
        var missing = await forge.FileText(_pull, "nope.cs", Head, Ct);

        Assert.Equal(ForgeErrors.NotFound.Code, directory.Error.Code);
        Assert.Equal(ForgeErrors.NotFound.Code, missing.Error.Code);
    }

    [Fact]
    public async Task Get_lists_every_page_of_check_runs_with_app_conclusion_and_sha()
    {
        const string first = """{"total_count":3,"check_runs":[{"id":1,"name":"build","status":"completed","conclusion":"success","head_sha":"abc123","app":{"slug":"github-actions"}},{"id":2,"name":"lint","status":"completed","conclusion":"failure","head_sha":"abc123","app":{"slug":"github-actions"}}]}""";
        const string second = """{"total_count":3,"check_runs":[{"id":3,"name":"e2e","status":"in_progress","conclusion":null,"head_sha":"abc123","app":{"slug":"circleci"}}]}""";
        var http = StubGet()
            .On(HttpMethod.Get, "/repos/acme/widgets/commits/abc123/check-runs?per_page=100&filter=latest&page=2", HttpStatusCode.OK, second)
            .On(HttpMethod.Get, "/repos/acme/widgets/commits/abc123/check-runs", HttpStatusCode.OK, first, ("Link", "<https://api.github.com/repos/acme/widgets/commits/abc123/check-runs?per_page=100&filter=latest&page=2>; rel=\"next\""));

        var checks = (await Build(http).GetPull(_pull, Ct)).Value.Checks;

        Assert.Equal(
            [new CheckRun("build", "github-actions", "success", Head), new CheckRun("lint", "github-actions", "failure", Head), new CheckRun("e2e", "circleci", "pending", Head)],
            checks.Runs);
        Assert.Equal(["lint"], checks.Failing);
        Assert.Equal(1, checks.Pending);
    }

    [Fact]
    public async Task Get_reads_a_commit_status_as_a_run()
    {
        const string status = """{"state":"failure","statuses":[{"context":"ci/legacy","state":"error"},{"context":"ci/deploy","state":"success"},{"context":"ci/slow","state":"pending"}]}""";
        var http = StubGet()
            .On(HttpMethod.Get, "/repos/acme/widgets/commits/abc123/status", HttpStatusCode.OK, status)
            .On(HttpMethod.Get, "/repos/acme/widgets/commits/abc123/check-runs", HttpStatusCode.OK, """{"check_runs":[]}""");

        var runs = (await Build(http).GetPull(_pull, Ct)).Value.Checks.Runs;

        Assert.Equal(
            [new CheckRun("ci/legacy", App: null, "failure", Head), new CheckRun("ci/deploy", App: null, "success", Head), new CheckRun("ci/slow", App: null, "pending", Head)],
            runs);
    }

    [Fact]
    public async Task SubmitReview_reads_the_head_first_and_posts_nothing_when_it_moved()
    {
        var http = ReviewStub();

        var outcome = await Build(http).SubmitReview(Review() with { ExpectedHead = "old999" }, Ct);

        Assert.Equal(ReviewOutcome.HeadMoved, outcome.Value);
        Assert.DoesNotContain(http.Sent, r => r.Method == HttpMethod.Post);
    }

    [Theory]
    [InlineData(ReviewVerdict.Comment, "COMMENT")]
    [InlineData(ReviewVerdict.Approve, "APPROVE")]
    [InlineData(ReviewVerdict.RequestChanges, "REQUEST_CHANGES")]
    public async Task SubmitReview_posts_the_commit_event_comments_and_a_hidden_marker(ReviewVerdict verdict, string eventName)
    {
        var http = ReviewStub().On(HttpMethod.Post, "/repos/acme/widgets/pulls/7/reviews", HttpStatusCode.OK, """{"id":9}""");

        var outcome = await Build(http).SubmitReview(Review() with { Verdict = verdict }, Ct);

        Assert.Equal(ReviewOutcome.Posted, outcome.Value);
        var (_, _, postBody) = Assert.Single(http.Sent, r => r.Method == HttpMethod.Post);
        using var body = System.Text.Json.JsonDocument.Parse(postBody);
        Assert.Equal(Head, body.RootElement.GetProperty("commit_id").GetString());
        Assert.Equal(eventName, body.RootElement.GetProperty("event").GetString());
        Assert.EndsWith("<!-- aw-marker:owner-review:d1:1 -->", body.RootElement.GetProperty("body").GetString(), StringComparison.Ordinal);
        Assert.StartsWith("Looks fine", body.RootElement.GetProperty("body").GetString(), StringComparison.Ordinal);
        var comment = Assert.Single(body.RootElement.GetProperty("comments").EnumerateArray());
        Assert.Equal("src/a.cs", comment.GetProperty("path").GetString());
        Assert.Equal(12, comment.GetProperty("line").GetInt32());
        Assert.Equal("Nit", comment.GetProperty("body").GetString());
    }

    [Fact]
    public async Task SubmitReview_with_a_marker_already_on_a_review_posts_nothing()
    {
        var http = new StubHandler()
            .On(HttpMethod.Get, "/repos/acme/widgets/pulls/7/reviews", HttpStatusCode.OK, """[{"id":9,"state":"COMMENTED","body":"Looks fine\n\n<!-- aw-marker:owner-review:d1:1 -->","user":{"login":"me"}}]""")
            .On(HttpMethod.Get, "/repos/acme/widgets/pulls/7", HttpStatusCode.OK, PullJson);

        var outcome = await Build(http).SubmitReview(Review(), Ct);

        Assert.Equal(ReviewOutcome.AlreadyDelivered, outcome.Value);
        Assert.DoesNotContain(http.Sent, r => r.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task SubmitReview_maps_a_422_on_a_line_to_LineRejected()
    {
        var http = ReviewStub().On(HttpMethod.Post, "/repos/acme/widgets/pulls/7/reviews", HttpStatusCode.UnprocessableEntity, """{"message":"Unprocessable Entity","errors":["Line could not be resolved"]}""");

        var outcome = await Build(http).SubmitReview(Review(), Ct);

        Assert.Equal(ReviewOutcome.LineRejected, outcome.Value);
    }

    [Fact]
    public async Task SubmitReview_does_not_swallow_other_failures()
    {
        var http = ReviewStub().On(HttpMethod.Post, "/repos/acme/widgets/pulls/7/reviews", HttpStatusCode.InternalServerError, "boom");

        var outcome = await Build(http).SubmitReview(Review(), Ct);

        Assert.Equal(ForgeErrors.Unavailable.Code, outcome.Error.Code);
    }

    [Fact]
    public async Task SubmitReview_with_a_marker_already_on_a_review_is_delivered_even_when_the_head_moved()
    {
        var http = new StubHandler()
            .On(HttpMethod.Get, "/repos/acme/widgets/pulls/7/reviews", HttpStatusCode.OK, """[{"id":9,"state":"COMMENTED","body":"Looks fine\n\n<!-- aw-marker:owner-review:d1:1 -->","user":{"login":"me"}}]""")
            .On(HttpMethod.Get, "/repos/acme/widgets/pulls/7", HttpStatusCode.OK, PullJson);

        var outcome = await Build(http).SubmitReview(Review() with { ExpectedHead = "old999" }, Ct);

        Assert.Equal(ReviewOutcome.AlreadyDelivered, outcome.Value);
    }

    [Fact]
    public async Task SubmitReview_maps_a_422_that_is_not_about_a_line_to_an_error()
    {
        var http = ReviewStub().On(HttpMethod.Post, "/repos/acme/widgets/pulls/7/reviews", HttpStatusCode.UnprocessableEntity, """{"message":"Unprocessable Entity","errors":["Can not approve your own pull request"]}""");
        var forge = Build(http);

        var noComments = await forge.SubmitReview(Review() with { Verdict = ReviewVerdict.Approve, Comments = [] }, Ct);
        var withComments = await forge.SubmitReview(Review() with { Verdict = ReviewVerdict.Approve }, Ct);

        Assert.Equal(ForgeErrors.Rejected.Code, noComments.Error.Code);
        Assert.Equal(ForgeErrors.Rejected.Code, withComments.Error.Code);
        Assert.Contains("Can not approve", noComments.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitReview_with_no_comments_does_not_read_a_422_about_a_path_as_a_line()
    {
        var http = ReviewStub().On(HttpMethod.Post, "/repos/acme/widgets/pulls/7/reviews", HttpStatusCode.UnprocessableEntity, """{"message":"Unprocessable Entity","errors":["Path could not be resolved"]}""");

        var outcome = await Build(http).SubmitReview(Review() with { Comments = [] }, Ct);

        Assert.Equal(ForgeErrors.Rejected.Code, outcome.Error.Code);
    }

    [Fact]
    public async Task Get_reads_every_page_of_commit_statuses()
    {
        string Page(int from, int count, string state = "success") =>
            $$"""{"state":"failure","statuses":[{{string.Join(',', Enumerable.Range(from, count).Select(i => Inv.S($$"""{"context":"ci/{{i}}","state":"{{(i == 31 ? "failure" : state)}}"}""")))}}]}""";
        var http = StubGet()
            .On(HttpMethod.Get, "/repos/acme/widgets/commits/abc123/status?per_page=100&page=2", HttpStatusCode.OK, Page(30, 2))
            .On(HttpMethod.Get, "/repos/acme/widgets/commits/abc123/status", HttpStatusCode.OK, Page(0, 30), Next("/repos/acme/widgets/commits/abc123/status", 2))
            .On(HttpMethod.Get, "/repos/acme/widgets/commits/abc123/check-runs", HttpStatusCode.OK, """{"check_runs":[]}""");

        var checks = (await Build(http).GetPull(_pull, Ct)).Value.Checks;

        Assert.Equal(32, checks.Runs!.Count);
        Assert.Equal(["ci/31"], checks.Failing);
        Assert.Equal(ChecksState.Failing, checks.State);
    }

    [Fact]
    public async Task FileText_asks_for_the_raw_media_type_and_caps_a_file_over_1_MiB()
    {
        var http = new StubHandler().OnRaw(HttpMethod.Get, "/repos/acme/widgets/contents/big.txt", Encoding.UTF8.GetBytes(new string('x', ForgeLimits.FileTextBytes + 5000)));

        var text = (await Build(http).FileText(_pull, "big.txt", Head, Ct)).Value;

        Assert.Equal(ForgeLimits.FileTextBytes, Encoding.UTF8.GetByteCount(text));
    }

    [Fact]
    public async Task FileText_of_bytes_that_are_not_UTF8_is_not_found()
    {
        var http = new StubHandler().OnRaw(HttpMethod.Get, "/repos/acme/widgets/contents/logo.png", [0x89, 0x50, 0xFF, 0xFE, 0x00, 0xC3, 0x28]);

        var text = await Build(http).FileText(_pull, "logo.png", Head, Ct);

        Assert.Equal(ForgeErrors.NotFound.Code, text.Error.Code);
    }

    [Fact]
    public async Task Files_says_the_cap_was_hit_when_the_pull_has_more_changed_files_than_it_lists()
    {
        var pageTwo = "/repos/acme/widgets/pulls/7/files?per_page=100&page=";
        var http = new StubHandler();
        for (var page = 30; page >= 2; page--)
        {
            _ = page == 30
                ? http.On(HttpMethod.Get, Inv.S($"{pageTwo}{page}"), HttpStatusCode.OK, FilesPage((page - 1) * 100, 100))
                : http.On(HttpMethod.Get, Inv.S($"{pageTwo}{page}"), HttpStatusCode.OK, FilesPage((page - 1) * 100, 100), Next("/repos/acme/widgets/pulls/7/files", page + 1));
        }

        _ = http
            .On(HttpMethod.Get, "/repos/acme/widgets/pulls/7/files", HttpStatusCode.OK, FilesPage(0, 100), Next("/repos/acme/widgets/pulls/7/files", 2))
            .On(HttpMethod.Get, "/repos/acme/widgets/pulls/7", HttpStatusCode.OK, PullJson.Replace("\"draft\":false", "\"draft\":false,\"changed_files\":4000", StringComparison.Ordinal));

        var files = (await Build(http).Files(_pull, Ct)).Value;

        Assert.Equal(ForgeLimits.PullFileCap, files.Files.Count);
        Assert.True(files.CapReached);
    }

    private static StubHandler StubGet() => new StubHandler()
        .On(HttpMethod.Get, "/repos/acme/widgets/pulls/7/reviews", HttpStatusCode.OK, "[]")
        .On(HttpMethod.Get, "/repos/acme/widgets/pulls/7", HttpStatusCode.OK, PullJson);

    private static StubHandler ReviewStub() => new StubHandler()
        .On(HttpMethod.Get, "/repos/acme/widgets/pulls/7/reviews", HttpStatusCode.OK, "[]")
        .On(HttpMethod.Get, "/repos/acme/widgets/pulls/7", HttpStatusCode.OK, PullJson);

    private static SubmitReviewRequest Review() =>
        new(_pull, Head, ReviewVerdict.Comment, "Looks fine", [new ReviewComment("src/a.cs", 12, "Nit")], "owner-review:d1:1");

    private sealed class FixedToken : ICredentialSource
    {
        public ValueTask<Result<string>> Token(string host, bool refresh, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success("token-for-tests"));
    }
}
