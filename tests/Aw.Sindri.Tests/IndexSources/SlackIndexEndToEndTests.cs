using System.Text.Json;
using Aw.Connectors.Core.Slack;
using Aw.Connectors.Features;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Models;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Features;
using Aw.Context.Infrastructure;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Map;
using Aw.Context.Testing.Vectors;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Host.IndexSources;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.IndexSources;

/// <summary>
/// Slack through the owned index end to end (owned-index spec §5.3, §5.5, §4.4), in the shape of the Linear one: the real feed catalog with the Host's Slack feed added, sync, SQLite record store and lexical leg over a canned Slack
/// reader, the profile read by SettingsReader, then a context query. Nothing but Slack's reader, the clock and the token store is faked.
/// </summary>
[Trait("Lane", "integration")]
public sealed class SlackIndexEndToEndTests : IDisposable
{
    private const string Profile = """{"sources":{"slack":{"channels":[{"id":"C0001GEN","name":"general"}],"token":"keychain:sindri-index/slack","workspace":"example"}}}""";
    private static readonly DateTimeOffset _now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Directory.CreateTempSubdirectory("slack-e2e-").FullName;
    private readonly ServiceProvider _provider;
    private readonly string _rootTs = SlackTs.Format(_now.AddDays(-3));

    public SlackIndexEndToEndTests()
    {
        var secrets = new FakeSecretWriter();
        secrets.Stored["keychain:sindri-index/slack"] = "bot-token-value";
        var slack = new CannedSlack(_rootTs, SlackTs.Format(_now.AddDays(-3).AddMinutes(4)), SlackTs.Format(_now.AddDays(-2)));
        _provider = new ServiceCollection()
            .AddSingleton<TimeProvider>(new FakeTimeProvider(_now))
            .AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>))
            .AddSingleton(FixtureMap.Json)
            .AddSingleton<IIdGenerator>(new SequentialIdGenerator())
            .AddSingleton<IModelPort>(new FakeModelPort())
            .AddSingleton<Context.Core.Vectors.IChunkEmbedder>(new FakeChunkEmbedder())
            .AddSingleton<Context.Core.Vectors.IQueryEmbedder>(new FakeQueryEmbedder())
            .AddSingleton<Context.Core.Vectors.IVectorScan>(new FakeVectorScan())
            .AddContextCore()
            .AddContextFeatures()
            .AddContextInfrastructure(name => name == "AW_SINDRI_HOME" ? _home : null)
            .AddConnectorsFeatures()
            .AddSingleton<Context.Core.Sync.IExternalRecordFeeds>(sp => new SlackRecordFeeds(secrets, sp.GetRequiredService<ISlackConnectorFactory>(), (_, _) => slack))
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = false, ValidateScopes = true });
    }

    private static ContextScope Scope(string repoPath) => new(
        JsonDocument.Parse(Profile).RootElement.Clone(),
        [new ContextRepo(ContextRepoName.From("split-panes"), repoPath)],
        new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));

    public void Dispose()
    {
        _provider.Dispose();
        Directory.Delete(_home, recursive: true);
    }

    [Fact]
    public async Task A_tick_stores_the_channels_thread_and_a_human_query_finds_it_by_its_text_with_the_slack_permalink()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var services = _provider.CreateAsyncScope();
        var engine = services.ServiceProvider.GetRequiredService<IContextEngine>();
        var scope = Scope(_home);

        var ticked = await engine.Tick(scope, ct);
        var stored = await services.ServiceProvider.GetRequiredService<IRecordStore>().Get(RecordKey.From($"slack:C0001GEN/{_rootTs}"), ct);
        var answer = await engine.Query(new ContextQuery("webhook signing secret"), scope, ct);

        Assert.True(ticked.IsSuccess, ticked.IsError ? ticked.Error.Message : "");
        var source = Assert.Single(ticked.Value.Sources, s => s.Source.Value == "slack");
        Assert.Null(source.Code);
        var record = Assert.IsType<StoredRecord>(stored.Value);
        Assert.Equal("slack:C0001GEN", record.Audience?.Value);
        Assert.Null(record.Repo);
        Assert.Equal(RecordKind.Message, record.Kind);
        Assert.Equal($"https://example.slack.com/archives/C0001GEN/p{_rootTs.Replace(".", string.Empty, StringComparison.Ordinal)}", record.Url);
        Assert.Equal(2, (await services.ServiceProvider.GetRequiredService<IRecordStore>().Count(SourceId.From("slack"), ct)).Value.Records);   // the thread and the day record of the unthreaded message
        Assert.True(answer.IsSuccess, answer.IsError ? answer.Error.Message : "");
        var hit = Assert.Single(answer.Value.Cites.Values, c => c.Key.Value.StartsWith("slack:", StringComparison.Ordinal) && c.Key.Value.Contains("/" + _rootTs, StringComparison.Ordinal));
        Assert.Equal($"slack:C0001GEN/{_rootTs}", hit.Key.Value);
        Assert.Equal($"https://example.slack.com/archives/C0001GEN/p{_rootTs.Replace(".", string.Empty, StringComparison.Ordinal)}", hit.Permalink?.ToString());
    }

    private sealed class CannedSlack(string rootTs, string replyTs, string laterTs) : ISlackReader
    {
        public ValueTask<Result<SlackMessagePage>> History(string channel, string? oldest, string? latest, CancellationToken cancellationToken)
        {
            Assert.Equal("C0001GEN", channel);
            return ValueTask.FromResult<Result<SlackMessagePage>>(new SlackMessagePage(
                [new SlackMessage(laterTs, ThreadTs: null, "Bo", "Lunch is at noon", ReplyCount: 0, Subtype: null), new SlackMessage(rootTs, rootTs, "Ada", "Please rotate the webhook signing secret today", ReplyCount: 1, Subtype: null)],
                RateLimitedUntil: null));
        }

        public ValueTask<Result<SlackMessagePage>> Replies(string channel, string threadTs, CancellationToken cancellationToken)
        {
            Assert.Equal("C0001GEN", channel);
            return ValueTask.FromResult<Result<SlackMessagePage>>(new SlackMessagePage(
                [new SlackMessage(rootTs, rootTs, "Ada", "Please rotate the webhook signing secret today", ReplyCount: 1, Subtype: null), new SlackMessage(replyTs, rootTs, "Bo", "Done, the new secret is live", ReplyCount: 0, Subtype: null)],
                RateLimitedUntil: null));
        }

        public ValueTask<Result<DateTimeOffset?>> Probe(string channel, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success<DateTimeOffset?>(value: null));
    }
}
