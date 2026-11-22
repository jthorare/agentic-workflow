using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Messages;
using Aw.Sindri.Features.ResolveLinks;

namespace Aw.Sindri.Tests.Messages;

/// <summary>M1g (messages spec §9.3, #3021): a link in message text resolves to its row key and the row's fields from the local store, or 'none'.</summary>
[Lane(TestLane.Unit)]
public sealed class ResolveLinksTests
{
    private const string PrKey = "gh:pr:acme/harbor#3120";

    private static readonly DateTimeOffset _since = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static LinkRow Pr => new(PrKey, "Fare table uses 90 s", "open")
    {
        Ci = "passing",
        WaitsOn = "you",
        Since = _since,
    };

    [Fact]
    public async Task Resolve_github_pr_url_to_row_key()
    {
        var url = new Uri("https://github.com/acme/harbor/pull/3120");

        var answer = await new ResolveLinksService(new Rows(Pr)).Handle(new ResolveLinksRequest([url]), Token);

        Assert.True(answer.IsSuccess);
        var link = Assert.Single(answer.Value.Links);
        Assert.Equal((url, PrKey), (link.Url, link.RowKey));
        Assert.Equal(("Fare table uses 90 s", "open", "passing", "you"), (link.Title, link.State, link.Ci, link.WaitsOn));
        Assert.Equal(_since, link.Since);
    }

    [Theory]
    [InlineData("https://github.com/acme/harbor/pull/3120", "gh:pr:acme/harbor#3120")]
    [InlineData("https://github.com/acme/harbor/pull/3120/files", "gh:pr:acme/harbor#3120")]
    [InlineData("https://github.com/acme/harbor/pull/3120#discussion_r1", "gh:pr:acme/harbor#3120")]
    [InlineData("http://www.github.com/acme/harbor/pull/7?w=1", "gh:pr:acme/harbor#7")]
    [InlineData("https://GitHub.com/acme/harbor.js/issues/3055", "gh:issue:acme/harbor.js#3055")]
    [InlineData("https://linear.app/acme/issue/eng-412/fare-table", "linear:issue:ENG-412")]
    [InlineData("https://linear.app/acme/issue/ENG-412", "linear:issue:ENG-412")]
    public void A_known_shape_gives_its_row_key(string url, string key) => Assert.Equal(key, LinkShapes.RowKey(new Uri(url)));

    [Theory]
    [InlineData("https://github.com/acme/harbor")]
    [InlineData("https://github.com/acme/harbor/pull/")]
    [InlineData("https://github.com/acme/harbor/pull/abc")]
    [InlineData("https://github.com/acme/harbor/pull/0123")]
    [InlineData("https://github.com/acme/harbor/commit/3120")]
    [InlineData("https://github.com/.acme/harbor/pull/3120")]
    [InlineData("https://gist.github.com/acme/harbor/pull/3120")]
    [InlineData("https://github.com.evil.example/acme/harbor/pull/3120")]
    [InlineData("ftp://github.com/acme/harbor/pull/3120")]
    [InlineData("https://linear.app/acme/issue/412")]
    [InlineData("https://linear.app/acme/project/eng-412")]
    [InlineData("https://example.com/acme/harbor/pull/3120")]
    public void Any_other_url_has_no_row_key(string url) => Assert.Null(LinkShapes.RowKey(new Uri(url)));

    [Fact]
    public async Task A_url_with_no_shape_and_a_key_the_store_lacks_both_answer_none_in_the_request_order()
    {
        Uri[] urls = [new("https://example.com/x"), new("https://github.com/acme/harbor/pull/3120"), new("https://github.com/acme/harbor/issues/1")];

        var answer = await new ResolveLinksService(new Rows(Pr)).Handle(new ResolveLinksRequest(urls), Token);

        Assert.Equal(urls, answer.Value.Links.Select(static l => l.Url));
        Assert.Equal([null, PrKey, null], answer.Value.Links.Select(static l => l.RowKey), StringComparer.Ordinal);
        Assert.All(answer.Value.Links.Where(static l => l.RowKey is null), static l => Assert.Equal<(string?, string?, string?)>((null, null, null), (l.Title, l.State, l.Ci)));
    }

    [Fact]
    public async Task A_repeated_url_reads_the_store_once_and_answers_each_time()
    {
        var rows = new Rows(Pr);
        var url = new Uri("https://github.com/acme/harbor/pull/3120");

        var answer = await new ResolveLinksService(rows).Handle(new ResolveLinksRequest([url, url, new Uri("https://github.com/acme/harbor/pull/3120/files")]), Token);

        Assert.Equal([PrKey, PrKey, PrKey], answer.Value.Links.Select(static l => l.RowKey), StringComparer.Ordinal);
        Assert.Equal([PrKey], Assert.Single(rows.Asked), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Urls_with_no_shape_never_read_the_store()
    {
        var rows = new Rows(Pr);

        var answer = await new ResolveLinksService(rows).Handle(new ResolveLinksRequest([new Uri("https://example.com/a"), new Uri("https://example.com/b")]), Token);

        Assert.Equal(2, answer.Value.Links.Count);
        Assert.Empty(rows.Asked);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public async Task No_url_or_more_than_twenty_is_refused(int count)
    {
        var urls = Enumerable.Range(0, count).Select(static i => new Uri(string.Create(CultureInfo.InvariantCulture, $"https://github.com/acme/harbor/pull/{i + 1}"))).ToList();

        var answer = await new ResolveLinksService(new Rows()).Handle(new ResolveLinksRequest(urls), Token);

        Assert.Equal("SND-API-013", answer.Error.Code);
    }

    [Fact]
    public async Task A_store_failure_is_the_answer()
    {
        var failing = new Rows { Failure = new Error("SND-TEST-001", ErrorKind.Unavailable, "the store is down") };

        var answer = await new ResolveLinksService(failing).Handle(new ResolveLinksRequest([new Uri("https://github.com/acme/harbor/pull/3120")]), Token);

        Assert.Equal("SND-TEST-001", answer.Error.Code);
    }

    // why: a test double of the one-method port: the rows it holds, and each key set it was asked for
    private sealed class Rows(params LinkRow[] held) : ILinkRows
    {
        public List<IReadOnlyCollection<string>> Asked { get; } = [];

        public Error? Failure { get; init; }

        public ValueTask<Result<IReadOnlyDictionary<string, LinkRow>>> Find(IReadOnlyCollection<string> keys, CancellationToken cancellationToken)
        {
            Asked.Add(keys);
            if (Failure is { } failure)
            {
                return ValueTask.FromResult<Result<IReadOnlyDictionary<string, LinkRow>>>(failure);
            }

            Result<IReadOnlyDictionary<string, LinkRow>> found = held.Where(r => keys.Contains(r.Key, StringComparer.Ordinal)).ToDictionary(static r => r.Key, StringComparer.Ordinal);
            return ValueTask.FromResult(found);
        }
    }
}
