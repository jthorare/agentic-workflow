using System.Net;
using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Host;
using Aw.Sindri.Testing.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.IndexSources;

// #2508: a Linear connected on the Connections page (OAuth) must feed the index with no profile token pointer, and win over a pointer, the order GitHub has had since #2499.
// The Linear feed is built inside Context, so this runs the Host's own composition and reads which Authorization header the feed sends to a loopback server.
public sealed class LinearTokenOrderTests : IDisposable
{
    private const string ProfileKey = "LINEAR_PROFILE_KEY";

    private readonly string _home = Directory.CreateTempSubdirectory("linear-order-").FullName;
    private readonly InMemoryConnectionStore _connections = new();
    private readonly FakeConnectionSecrets _connectionSecrets = new();
    private readonly List<string> _authorizations = [];
    private readonly HttpListener _listener;
    private readonly string _url;
    private readonly CancellationTokenSource _stop = new();

    public LinearTokenOrderTests()
    {
        var (listener, port) = ChosenPortListener.Start();
        _listener = listener;
        _url = $"http://127.0.0.1:{port.ToString(System.Globalization.CultureInfo.InvariantCulture)}/graphql";
        _ = Task.Run(Serve, _stop.Token);
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Close();
        _stop.Dispose();
        Directory.Delete(_home, recursive: true);
    }

    private async Task Serve()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync().WaitAsync(_stop.Token);
                lock (_authorizations)
                {
                    _authorizations.Add(context.Request.Headers["Authorization"] ?? string.Empty);
                }

                var body = "{\"data\":{}}"u8.ToArray();
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = body.Length;
                await context.Response.OutputStream.WriteAsync(body, _stop.Token);
                context.Response.Close();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException or OperationCanceledException)
            {
                return;
            }
        }
    }

    private string WithPointer => """{"sources":{"linear":{"url":"URL","teams":["ENG"],"token":"env:KEY"}}}""".Replace("URL", _url, StringComparison.Ordinal).Replace("KEY", ProfileKey, StringComparison.Ordinal);

    private string WithoutPointer => """{"sources":{"linear":{"url":"URL","teams":["ENG"]}}}""".Replace("URL", _url, StringComparison.Ordinal);

    private const string NoLinear = """{"sources":{"linear":{"teams":["ENG"]}}}""";

    private static Scrubbers Scrub() => new(new PassFilter(), new PassFilter(), ScrubRuleset.From("0123456789abcdef"));

    private async Task Connect(ConnectionStatus status)
    {
        var connection = ConnectionSamples.Oauth(ConnectionSamples.Name("linear", "work"), status);
        _ = (await _connections.Insert(connection, CancellationToken.None)).Value;
        _ = _connectionSecrets.Store(connection.Name, new TokenSet("lin_oauth_connection", RefreshToken: null, ExpiresAt: null)).Value;
    }

    private IReadOnlyList<RecordFeed> LinearFeeds(string profile)
    {
        var read = SettingsReader.Read(JsonDocument.Parse(profile).RootElement.Clone());
        Assert.True(read.IsSuccess, read.IsError ? read.Error.Message : "");
        string? Environment(string name) => name switch
        {
            "AW_STATE_DIR" or "AW_SINDRI_HOME" => _home,
            ProfileKey => "lin_profile_key",
            _ => null,
        };

        var services = Composition.Compose(new ServiceCollection(), Environment, new FakeTimeProvider())
            .AddSingleton<IConnectionStore>(_connections)
            .AddSingleton<IConnectionSecrets>(_connectionSecrets);
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = false, ValidateScopes = true });
        var repo = new ContextRepo(ContextRepoName.From("split-panes"), _home);
        return [.. provider.GetRequiredService<IRecordFeedCatalog>().For(read.Value, [repo], Scrub()).Where(static f => f.Source.Value == "linear")];
    }

    private async Task<string> FirstAuthorization(IReadOnlyList<RecordFeed> feeds)
    {
        var feed = feeds[0];
        _ = await feed.Pull(new RecordPull(After: null, Limit: 10, NotBefore: null), TestContext.Current.CancellationToken);
        lock (_authorizations)
        {
            return Assert.Single(_authorizations.Take(1));
        }
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_linear_feed_reads_with_the_connected_linear_connection_even_when_the_profile_token_resolves()
    {
        await Connect(ConnectionStatus.Connected);

        var feeds = LinearFeeds(WithPointer);

        Assert.NotEmpty(feeds);
        Assert.EndsWith("lin_oauth_connection", await FirstAuthorization(feeds), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_linear_feed_exists_and_reads_with_the_connected_linear_connection_when_the_profile_has_no_token_pointer()
    {
        await Connect(ConnectionStatus.Connected);

        var feeds = LinearFeeds(WithoutPointer);

        Assert.NotEmpty(feeds);
        Assert.EndsWith("lin_oauth_connection", await FirstAuthorization(feeds), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_linear_feed_falls_back_to_the_profile_token_when_the_connection_needs_sign_in()
    {
        await Connect(ConnectionStatus.NeedsSignIn);

        var feeds = LinearFeeds(WithPointer);

        Assert.NotEmpty(feeds);
        Assert.Equal("lin_profile_key", await FirstAuthorization(feeds));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_linear_feed_reads_with_the_profile_token_when_there_is_no_linear_connection()
    {
        var feeds = LinearFeeds(WithPointer);

        Assert.NotEmpty(feeds);
        Assert.Equal("lin_profile_key", await FirstAuthorization(feeds));
    }

    [Fact, Trait("Lane", "integration")]
    public void There_is_no_linear_feed_with_no_linear_connection_and_no_profile_token_pointer()
        => Assert.Empty(LinearFeeds(NoLinear));

    private sealed class PassFilter : IIngestFilter, IEgressScrubber
    {
        public ScrubbedField Clean(string raw, bool singleLine) => new(raw, Redacted: false, Blocked: false);

        public ScrubbedField Scrub(string value) => Clean(value, singleLine: false);
    }
}
