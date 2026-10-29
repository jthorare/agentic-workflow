using System.Text.Json;
using Aw.Connectors.Core;
using Aw.Connectors.Core.Slack;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Host.IndexSources;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.IndexSources;

public sealed class SlackConnectTests
{
    private const string Planted = "bot-planted-secret-0123456789";
    private const string Listed = """{"sources":{"slack":{"channels":[{"id":"C0001GEN","name":"general"},{"id":"G0002OPS","name":"ops"}],"workspace":"example"}}}""";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly FakeSecretPrompts _prompts = new();
    private readonly FakeSecretWriter _secrets = new();
    private readonly InMemoryProfiles _profiles = new();
    private readonly RecordingProfileWriter _written = new();
    private readonly ScriptedReader _reader = new();
    private readonly List<(string Token, string[] Channels)> _built = [];

    public SlackConnectTests()
    {
        _profiles.Live = Profile(Listed);
        _prompts.Answer(Planted);
    }

    private SlackConnect Connect(FakeSecretPrompts? prompts = null) => new(prompts ?? _prompts, _profiles, _secrets, _secrets, _written, (token, channels) =>
    {
        _built.Add((token, [.. channels]));
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
    public async Task A_profile_with_no_slack_channels_asks_nothing_and_sends_nothing()
    {
        _profiles.Live = Profile("""{"sources":{}}""");

        var result = await Connect().Run(human: true, Ct);

        Assert.Equal(SindriErrors.IndexNotInProfile.Code, result.Error.Code);
        Assert.Empty(_prompts.Asked);
        Assert.Empty(_reader.Probed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_token_is_asked_with_the_echo_off_every_listed_channel_is_probed_then_it_is_stored_and_pointed_to()
    {
        var result = await Connect().Run(human: true, Ct);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        Assert.Equal(1, _prompts.SecretsAsked);
        Assert.Equal(["C0001GEN", "G0002OPS"], _reader.Probed, StringComparer.Ordinal);
        var (builtToken, builtChannels) = Assert.Single(_built);
        Assert.Equal(Planted, builtToken);
        Assert.Equal(["C0001GEN", "G0002OPS"], builtChannels, StringComparer.Ordinal);
        Assert.Equal(Planted, _secrets.Stored["keychain:sindri-index/slack"]);
        Assert.Equal(["keychain:sindri-index/slack"], _written.Pointers, StringComparer.Ordinal);
        Assert.Equal(("slack", 2), (_written.Sources[0], result.Value.Channels));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_direct_message_in_the_profile_is_refused_when_the_profile_is_read_so_connect_never_asks_for_a_token()
    {
        _profiles.Live = Profile("""{"sources":{"slack":{"channels":[{"id":"C0001GEN","name":"general"},{"id":"D0003DMX","name":"a-dm"}],"workspace":"example"}}}""");

        var result = await Connect().Run(human: true, Ct);

        // The settings reader refuses a D… id at load (SND-CONTEXT-001), earlier than connect and for every caller; connect's own 013 check is the second line.
        Assert.Equal("SND-CONTEXT-001", result.Error.Code);
        Assert.Contains("context.sources.slack.channels", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_prompts.Asked);
        Assert.Empty(_reader.Probed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_channel_the_reader_refuses_as_not_allowed_saves_nothing_and_comes_back_as_013()
    {
        _reader.Failures["G0002OPS"] = ConnectorsErrors.ChannelNotAllowed.With("not in the profile");

        var result = await Connect().Run(human: true, Ct);

        Assert.Equal("SND-INDEX-013", result.Error.Code);
        Assert.Empty(_secrets.Stored);
        Assert.Empty(_written.Pointers);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task One_unreadable_channel_saves_nothing_and_is_named()
    {
        _reader.Failures["G0002OPS"] = ConnectorsErrors.AccessDenied.With("not_in_channel");

        var result = await Connect().Run(human: true, Ct);

        Assert.Equal(SindriErrors.IndexRepoUnreadable.Code, result.Error.Code);
        Assert.Contains("ops", result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("general", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_secrets.Stored);
        Assert.Empty(_written.Pointers);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_refused_token_saves_nothing_ends_the_check_at_once_and_is_never_repeated()
    {
        _reader.Failures["C0001GEN"] = ConnectorsErrors.Unauthorized.With("invalid_auth " + Planted);

        var result = await Connect().Run(human: true, Ct);

        Assert.Equal(SindriErrors.IndexTokenUnresolved.Code, result.Error.Code);
        Assert.DoesNotContain(Planted, result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(["C0001GEN"], _reader.Probed, StringComparer.Ordinal);
        Assert.Empty(_secrets.Stored);
        Assert.Empty(_written.Pointers);
        Assert.DoesNotContain(_prompts.Asked, q => q.Contains(Planted, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rate_limit_during_the_check_saves_nothing()
    {
        _reader.LimitedUntil = new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero);

        var result = await Connect().Run(human: true, Ct);

        Assert.Equal(ConnectorsErrors.RateLimited.Code, result.Error.Code);
        Assert.Empty(_secrets.Stored);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_empty_answer_saves_nothing()
    {
        var prompts = new FakeSecretPrompts();
        prompts.Answer("   ");

        var result = await Connect(prompts).Run(human: true, Ct);

        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
        Assert.Empty(_reader.Probed);
        Assert.Empty(_secrets.Stored);
    }

    private sealed class RecordingProfileWriter : ISourceTokenProfileWriter
    {
        public List<string> Pointers { get; } = [];

        public List<string> Sources { get; } = [];

        public Result<bool> SetTeams(string source, IReadOnlyList<string> teams) => throw new NotSupportedException();

        public Result<bool> SetToken(string source, string secretPointer)
        {
            Sources.Add(source);
            Pointers.Add(secretPointer);
            return true;
        }
    }

    private sealed class ScriptedReader : ISlackReader
    {
        public List<string> Probed { get; } = [];

        public Dictionary<string, Error> Failures { get; } = new(StringComparer.Ordinal);

        public DateTimeOffset? LimitedUntil { get; set; }

        public ValueTask<Result<DateTimeOffset?>> Probe(string channel, CancellationToken cancellationToken)
        {
            Probed.Add(channel);
            return ValueTask.FromResult(Failures.TryGetValue(channel, out var failure) ? Results.Failure<DateTimeOffset?>(failure) : Results.Success(LimitedUntil));
        }

        public ValueTask<Result<SlackMessagePage>> History(string channel, string? oldest, string? latest, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<SlackMessagePage>> Replies(string channel, string threadTs, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
