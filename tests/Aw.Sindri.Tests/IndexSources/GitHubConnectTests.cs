using System.Text.Json;
using Aw.Connectors.Core;
using Aw.Connectors.Core.GitHub;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Host.IndexSources;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.IndexSources;

public sealed class GitHubConnectTests
{
    private const string Planted = "planted-secret-0123456789";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly FakeSecretPrompts _prompts = new();
    private readonly FakeSecretWriter _secrets = new();
    private readonly InMemoryProfiles _profiles = new();
    private readonly RecordingProfileWriter _written = new();
    private readonly ScriptedReader _reader = new();

    public GitHubConnectTests()
    {
        _profiles.Live = Profile("""{"sources":{"github":{"repos":["acme/widgets","acme/gadgets"]}}}""");
        _prompts.Answer(Planted);
    }

    private GitHubConnect Connect(FakeSecretPrompts? prompts = null) => new(prompts ?? _prompts, _profiles, _secrets, _secrets, _written, (_, _, _) => _reader, TimeProvider.System);

    private static LoadedProfile Profile(string contextJson)
    {
        var context = JsonDocument.Parse(contextJson).RootElement.Clone();
        var profile = new SindriProfile(1, "thor", Context: context);
        return new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_is_refused_before_anything_is_asked_or_saved()
    {
        var result = await Connect().Run(human: false, Ct);

        Assert.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
        Assert.Empty(_prompts.Asked);
        Assert.Empty(_secrets.Stored);
        Assert.Empty(_written.Pointers);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_profile_with_no_github_repos_asks_nothing_and_sends_nothing()
    {
        _profiles.Live = Profile("""{"sources":{}}""");

        var result = await Connect().Run(human: true, Ct);

        Assert.Equal(SindriErrors.IndexNotInProfile.Code, result.Error.Code);
        Assert.Empty(_prompts.Asked);
        Assert.Empty(_reader.Checked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_token_is_asked_with_the_echo_off_checked_against_every_repo_then_stored_and_pointed_to()
    {
        var result = await Connect().Run(human: true, Ct);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        Assert.Equal(1, _prompts.SecretsAsked);
        Assert.Equal(["acme/widgets", "acme/gadgets"], _reader.Checked);
        Assert.Equal(Planted, _secrets.Stored["keychain:sindri-index/github"]);
        Assert.Equal(["keychain:sindri-index/github"], _written.Pointers);
        Assert.Equal("keychain:sindri-index/github", result.Value.Pointer);
        Assert.Equal(2, result.Value.Repos);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task One_unreadable_repo_saves_nothing_and_is_named()
    {
        _reader.Failures["acme/gadgets"] = ConnectorsErrors.NotFound.With("no such repo");

        var result = await Connect().Run(human: true, Ct);

        Assert.Equal(SindriErrors.IndexRepoUnreadable.Code, result.Error.Code);
        Assert.Contains("acme/gadgets", result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("acme/widgets", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_secrets.Stored);
        Assert.Empty(_written.Pointers);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_refused_token_saves_nothing()
    {
        _reader.Failures["acme/widgets"] = ConnectorsErrors.Unauthorized.With("401");

        var result = await Connect().Run(human: true, Ct);

        Assert.Equal(SindriErrors.IndexTokenUnresolved.Code, result.Error.Code);
        Assert.Empty(_secrets.Stored);
        Assert.Empty(_written.Pointers);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_result_or_prompt_repeats_the_token()
    {
        _reader.Failures["acme/gadgets"] = ConnectorsErrors.NotFound.With("no such repo");
        var failed = await Connect().Run(human: true, Ct);
        _prompts.Answer(Planted);
        _reader.Failures.Clear();
        var ok = await Connect().Run(human: true, Ct);

        Assert.DoesNotContain(Planted, failed.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Planted, ok.Value.Pointer, StringComparison.Ordinal);
        Assert.DoesNotContain(_prompts.Asked, q => q.Contains(Planted, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_empty_answer_saves_nothing()
    {
        var prompts = new FakeSecretPrompts();
        prompts.Answer("   ");

        var result = await Connect(prompts).Run(human: true, Ct);

        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
        Assert.Empty(_reader.Checked);
        Assert.Empty(_secrets.Stored);
    }

    private sealed class RecordingProfileWriter : ISourceTokenProfileWriter
    {
        public List<string> Pointers { get; } = [];

        public Result<bool> SetToken(string source, string pointer)
        {
            Pointers.Add(pointer);
            return true;
        }
    }

    private sealed class ScriptedReader : IGitHubReader
    {
        public List<string> Checked { get; } = [];

        public Dictionary<string, Error> Failures { get; } = new(StringComparer.Ordinal);

        public ValueTask<Result<GitHubPullPage>> Issues(string repo, GitHubCursor after, int limit, DateTimeOffset? before, CancellationToken cancellationToken)
        {
            Checked.Add(repo);
            return ValueTask.FromResult<Result<GitHubPullPage>>(Failures.TryGetValue(repo, out var failure) ? failure : new GitHubPullPage([], after, RateLimitedUntil: null));
        }

        public ValueTask<Result<GitHubCommentPage>> Comments(string repo, DateTimeOffset since, DateTimeOffset until, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<GitHubCommentPage>> IssueComments(string repo, int number, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<GitHubPullDetailRead>> PullRequest(string repo, int number, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
