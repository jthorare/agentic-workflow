using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Features.ListReviews;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Forge;

namespace Aw.Sindri.Tests.Reviews;

/// <summary>The login prompt says which connection exists and why its login is missing (#2936).</summary>
public sealed class ReviewLoginPromptTests
{
    private readonly FakeGitHubAccount _account = new();

    private ReviewAccountReader Reader() => new(_account, new FakePullRequests(["github.com"]));

    [Fact, Trait("Lane", "unit")]
    public async Task Identity_carries_the_connection_name_and_the_miss()
    {
        _account.State = GitHubAccountState.Connected;
        _account.Connection = "github:sam-rivera";
        _account.Miss = GitHubLoginMiss.SignIn;

        var who = (await Reader().Identity(profile: null, lastCode: null, CancellationToken.None)).Value;

        Assert.Null(who.Login);
        Assert.Equal("github:sam-rivera", who.Connection);
        Assert.Equal(GitHubLoginMiss.SignIn, who.Miss);
    }

    [Fact, Trait("Lane", "unit")]
    public void For_carries_the_connection_and_reason()
    {
        var prompt = ReviewLoginPrompt.For(suggested: null, "github:sam-rivera", GitHubLoginMiss.Unreachable);

        Assert.Equal("github:sam-rivera", prompt.Connection);
        Assert.Equal(GitHubLoginMiss.Unreachable, prompt.Reason);
    }

    [Fact, Trait("Lane", "unit")]
    public void For_without_a_connection_says_none()
    {
        var prompt = ReviewLoginPrompt.For("joi");

        Assert.Null(prompt.Connection);
        Assert.Equal(GitHubLoginMiss.None, prompt.Reason);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(GitHubLoginMiss.None, "none")]
    [InlineData(GitHubLoginMiss.Unreachable, "unreachable")]
    [InlineData(GitHubLoginMiss.SignIn, "signIn")]
    [InlineData(GitHubLoginMiss.UnsupportedHost, "unsupportedHost")]
    public void Reason_is_a_camelCase_word_on_the_wire(GitHubLoginMiss miss, string word)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(miss);

        Assert.Equal($"\"{word}\"", json);
    }
}
