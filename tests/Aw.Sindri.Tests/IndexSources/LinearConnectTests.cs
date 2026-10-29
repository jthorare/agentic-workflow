using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Linear;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Host.IndexSources;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.IndexSources;

public sealed class LinearConnectTests
{
    private const string Planted = "lin_api_planted_secret_0123456789";
    private const string Listed = """{"sources":{"linear":{"teams":["ENG","APP"]}}}""";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly FakeSecretPrompts _prompts = new();
    private readonly FakeSecretWriter _secrets = new();
    private readonly InMemoryProfiles _profiles = new();
    private readonly RecordingProfileWriter _written = new();
    private readonly ScriptedReader _reader = new();
    private readonly List<(Uri Url, string Key)> _built = [];

    public LinearConnectTests()
    {
        _profiles.Live = Profile(Listed);
        _prompts.Answer(Planted);
    }

    private LinearConnect Connect(FakeSecretPrompts? prompts = null) => new(prompts ?? _prompts, _profiles, _secrets, _secrets, _written, (url, key) =>
    {
        _built.Add((url, key));
        return _reader;
    });

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
        Assert.Empty(_built);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_key_is_asked_with_the_echo_off_checked_against_the_teams_then_stored_and_pointed_to()
    {
        _reader.Readable = ["ENG", "APP", "OPS"];

        var result = await Connect().Run(human: true, Ct);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        Assert.Equal(1, _prompts.SecretsAsked);
        var (url, key) = Assert.Single(_built);
        Assert.Equal(new Uri("https://api.linear.app/graphql"), url);
        Assert.Equal(Planted, key);
        Assert.Equal(Planted, _secrets.Stored["keychain:sindri-index/linear"]);
        Assert.Equal(["keychain:sindri-index/linear"], _written.Pointers, StringComparer.Ordinal);
        Assert.Equal(("linear", 2), (_written.Sources[0], result.Value.Teams));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_loopback_url_is_the_one_the_reader_is_built_for_and_its_key_goes_to_the_file()
    {
        _profiles.Live = Profile("""{"sources":{"linear":{"teams":["ENG"],"url":"http://127.0.0.1:7777/graphql","token":"env:LINEAR_FAKE"}}}""");
        _reader.Readable = ["ENG"];

        var result = await Connect().Run(human: true, Ct);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        Assert.Equal(new Uri("http://127.0.0.1:7777/graphql"), Assert.Single(_built).Url);
        Assert.Equal(["file:/fake-secrets/sindri-index-linear"], _written.Pointers, StringComparer.Ordinal);
        Assert.True(LinearSettings.Create("http://127.0.0.1:7777/graphql", _written.Pointers[0], ["ENG"]).IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_listed_team_the_key_cannot_read_saves_nothing_and_is_named()
    {
        _reader.Readable = ["ENG"];

        var result = await Connect().Run(human: true, Ct);

        Assert.Equal(SindriErrors.IndexRepoUnreadable.Code, result.Error.Code);
        Assert.Contains("APP", result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ENG", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_secrets.Stored);
        Assert.Empty(_written.Pointers);
        Assert.Empty(_written.Teams);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_listed_teams_and_none_readable_saves_nothing()
    {
        _profiles.Live = Profile("""{"sources":{}}""");
        _reader.Readable = [];

        var result = await Connect().Run(human: true, Ct);

        Assert.Equal(SindriErrors.IndexRepoUnreadable.Code, result.Error.Code);
        Assert.Empty(_secrets.Stored);
        Assert.Empty(_written.Pointers);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_listed_teams_and_one_readable_is_connected()
    {
        _profiles.Live = Profile("""{"sources":{}}""");
        _reader.Readable = ["ENG", "APP"];

        var result = await Connect().Run(human: true, Ct);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        Assert.Equal(2, result.Value.Teams);
        Assert.Equal(["keychain:sindri-index/linear"], _written.Pointers, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_listed_teams_writes_the_teams_the_key_reads_sorted_with_the_pointer()
    {
        _profiles.Live = Profile("""{"sources":{}}""");
        _reader.Readable = ["OPS", "ENG"];

        var result = await Connect().Run(human: true, Ct);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        Assert.Equal(["ENG", "OPS"], result.Value.Added);
        var (source, teams) = Assert.Single(_written.Teams);
        Assert.Equal("linear", source);
        Assert.Equal(["ENG", "OPS"], teams);
        Assert.Equal(["keychain:sindri-index/linear"], _written.Pointers, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Listed_teams_are_left_alone()
    {
        _reader.Readable = ["ENG", "APP", "OPS"];

        var result = await Connect().Run(human: true, Ct);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        Assert.Empty(result.Value.Added);
        Assert.Empty(_written.Teams);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_refused_key_saves_nothing_and_is_never_repeated()
    {
        _reader.Failure = ContextErrors.LinearKeyRefused.With("refused " + Planted);

        var result = await Connect().Run(human: true, Ct);

        Assert.Equal(SindriErrors.IndexTokenUnresolved.Code, result.Error.Code);
        Assert.DoesNotContain(Planted, result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_secrets.Stored);
        Assert.Empty(_written.Pointers);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unreachable_Linear_saves_nothing()
    {
        _reader.Failure = ContextErrors.SourceUnavailable.With("down");

        var result = await Connect().Run(human: true, Ct);

        Assert.Equal(ContextErrors.SourceUnavailable.Code, result.Error.Code);
        Assert.Empty(_secrets.Stored);
        Assert.Empty(_written.Pointers);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_empty_answer_saves_nothing()
    {
        var prompts = new FakeSecretPrompts();
        prompts.Answer("   ");

        var result = await Connect(prompts).Run(human: true, Ct);

        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
        Assert.Empty(_built);
        Assert.Empty(_secrets.Stored);
    }

    private sealed class RecordingProfileWriter : ISourceTokenProfileWriter
    {
        public List<string> Pointers { get; } = [];

        public List<string> Sources { get; } = [];

        public List<(string Source, string[] Teams)> Teams { get; } = [];

        public Result<bool> SetTeams(string source, IReadOnlyList<string> teams)
        {
            Teams.Add((source, [.. teams]));
            return true;
        }

        public Result<bool> SetToken(string source, string secretPointer)
        {
            Sources.Add(source);
            Pointers.Add(secretPointer);
            return true;
        }
    }

    private sealed class ScriptedReader : ILinearTeamReader
    {
        public IReadOnlyList<string> Readable { get; set; } = [];

        public Error? Failure { get; set; }

        public ValueTask<Result<IReadOnlyList<string>>> Teams(CancellationToken cancellationToken) =>
            ValueTask.FromResult(Failure is { } failure ? Results.Failure<IReadOnlyList<string>>(failure) : Results.Success(Readable));
    }
}
