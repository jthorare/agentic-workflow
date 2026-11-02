using Aw.Connectors.Core;
using Aw.Connectors.Core.GitHub;
using Aw.Connectors.Features;
using Aw.Connectors.Testing;
using Aw.Connectors.Testing.GitHub;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Connectors.Tests.GitHub;

public sealed class GitHubConnectorTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static IConnector Connector(FakeGitHubReader reader, params string[] repos)
    {
        var provider = new ServiceCollection().AddSingleton<TimeProvider>(new FixedClock(_now)).AddSingleton<IGitHubReader>(reader).AddConnectorsFeatures().BuildServiceProvider();
        var settings = GitHubSettings.Create(repos, token: null, host: null, allowPrivateHost: false, commentCap: null).Value;
        return provider.GetRequiredService<IGitHubConnectorFactory>().Create(settings, reader, TimeSpan.FromDays(30));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Every_listed_repo_is_pulled_and_the_next_pull_continues_each_from_its_own_cursor()
    {
        var reader = new FakeGitHubReader()
            .Add("acme/widgets", A.GitHubIssue(1).UpdatedAt(_now.AddDays(-2)))
            .Add("acme/gadgets", A.GitHubIssue(2).UpdatedAt(_now.AddDays(-1)));
        var connector = Connector(reader, "acme/widgets", "acme/gadgets");

        var first = (await connector.Pull(after: null, limit: 10, before: null, Ct)).Value;
        _ = reader.Add("acme/widgets", A.GitHubIssue(3).UpdatedAt(_now.AddHours(-3)));
        var second = (await connector.Pull(first.Next, limit: 10, before: null, Ct)).Value;

        Assert.True(first.Items.Select(static i => i.Key).SequenceEqual(["github:issue/acme/widgets/1", "github:issue/acme/gadgets/2"], StringComparer.Ordinal));
        Assert.True(second.Items.Select(static i => i.Key).SequenceEqual(["github:issue/acme/widgets/3"], StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_first_pull_reaches_back_only_by_the_window()
    {
        var reader = new FakeGitHubReader()
            .Add("acme/widgets", A.GitHubIssue(1).UpdatedAt(_now.AddDays(-90)))
            .Add("acme/widgets", A.GitHubIssue(2).UpdatedAt(_now.AddDays(-5)));

        var page = (await Connector(reader, "acme/widgets").Pull(after: null, limit: 10, before: null, Ct)).Value;

        Assert.True(page.Items.Select(static i => i.Key).SequenceEqual(["github:issue/acme/widgets/2"], StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_limit_is_shared_across_repos()
    {
        var reader = new FakeGitHubReader()
            .Add("acme/widgets", A.GitHubIssue(1).UpdatedAt(_now.AddDays(-3)))
            .Add("acme/widgets", A.GitHubIssue(2).UpdatedAt(_now.AddDays(-2)))
            .Add("acme/gadgets", A.GitHubIssue(3).UpdatedAt(_now.AddDays(-1)));

        var page = (await Connector(reader, "acme/widgets", "acme/gadgets").Pull(after: null, limit: 2, before: null, Ct)).Value;

        Assert.Equal(2, page.Items.Count);
        Assert.DoesNotContain(reader.Calls, static c => c.Repo == "acme/gadgets");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rate_limit_with_nothing_read_is_an_error_row_that_names_when_it_lifts()
    {
        var until = _now.AddMinutes(30);
        var reader = new FakeGitHubReader().Add("acme/widgets", A.GitHubIssue(1).UpdatedAt(_now.AddDays(-1)).WithComments(2)).RateLimitComments(until);

        var result = await Connector(reader, "acme/widgets").Pull(after: null, limit: 10, before: null, Ct);

        Assert.True(result.IsError);
        Assert.Equal(ConnectorsErrors.RateLimited.Code, result.Error.Code);
        Assert.Contains("2026-10-27T12:30:00", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failing_repo_with_nothing_read_is_an_error_row()
    {
        var reader = new FakeGitHubReader().Add("acme/widgets", A.GitHubIssue(1).UpdatedAt(_now.AddDays(-1)));
        var connector = Connector(reader, "acme/widgets", "acme/gadgets");
        _ = reader.Fail(ConnectorsErrors.Unreachable.With("down"));

        var failing = await connector.Pull(after: null, limit: 10, before: null, Ct);

        Assert.True(failing.IsError);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cursor_that_does_not_parse_starts_that_repo_over()
    {
        var parsed = Features.PullGitHub.GitHubCursors.Parse("acme/widgets|notanumber|1;acme/gadgets|638000000000000000|4,5");

        Assert.True(parsed.Keys.SequenceEqual(["acme/gadgets"], StringComparer.Ordinal));
        Assert.Equal([4L, 5L], parsed["acme/gadgets"].SeenIds);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
