using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.AddConnection;
using Aw.Sindri.Features.GetConnection;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands;

/// <summary>
/// 'sindri connection add' reads --key-env and --client-secret-env in the CLI's own shell and posts the values. Each value has its own canary: it must reach the POST body (serve has to store it)
/// and nothing the verb prints on standard output or standard error, on success, on a server refusal and on an unset variable.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class CliConnectionVerbsTests : IDisposable
{
    private const string KeyCanary = "aw-canary-key-7f3a91c2";
    private const string ClientSecretCanary = "aw-canary-clientsecret-4be0d6a8";

    private static readonly FakeControlPlaneProbe _probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static IHumanTokenStore Human => InMemoryHumanTokenStore.WithToken();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    private static string? Shell(string name) => name switch
    {
        "ACME_KEY" => KeyCanary,
        "GH_CLIENT_SECRET" => ClientSecretCanary,
        _ => null,
    };

    private static AddConnectionResponse Added(string name, string kind) =>
        new(new ConnectionView(name, kind, Preset: null, "connected", StatusReason: null, new Dictionary<string, string>(StringComparer.Ordinal), LastRefreshedAt: null, HasSecret: true), ConsentUrl: null);

    private ConnectionVerbs Verbs(RoutedApi api)
    {
        var client = new ControlPlaneClient(_probe, SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), Human, _ => ValueTask.FromResult((RepoName?)null)), handler: api);
        return new ConnectionVerbs(client, new Output(_out, _error, SliceHarness.Json), (_, _) => Task.FromResult<Kernel.Error?>(null), Shell);
    }

    private void AssertNeitherPrinted()
    {
        foreach (var printed in new[] { _out.ToString(), _error.ToString() })
        {
            Assert.DoesNotContain(KeyCanary, printed, StringComparison.Ordinal);
            Assert.DoesNotContain(ClientSecretCanary, printed, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task The_key_is_read_from_this_shell_posted_and_never_printed()
    {
        var api = new RoutedApi().OnPost("/sindri/connections", HttpStatusCode.OK, Added("acme:key", "rest"));

        var code = await Verbs(api).Run(["connection", "add", "acme:key", "--kind", "rest", "--key-env", "ACME_KEY"], Token);

        Assert.Equal(0, code);
        var body = api.Calls.Single(static c => c.Method == "POST").Body;
        Assert.Contains(KeyCanary, body, StringComparison.Ordinal);   // serve has to receive it to store it
        Assert.DoesNotContain("ACME_KEY", body, StringComparison.Ordinal);   // the name would send serve looking in its own environment
        AssertNeitherPrinted();
        Assert.Contains("acme:key", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_client_secret_is_read_from_this_shell_posted_and_never_printed_even_when_the_wait_for_the_row_fails()
    {
        var signingIn = new AddConnectionResponse(Added("linear:work", "oauth").Connection with { Status = "needs-sign-in" }, "https://provider.example.test/authorize?state=x");
        var api = new RoutedApi().OnPost("/sindri/connections", HttpStatusCode.OK, signingIn);   // no GET route: the poll for the row ends with the API's own error

        var code = await Verbs(api).Run(["connection", "add", "linear:work", "--preset", "linear", "--client-id", "client-123", "--client-secret-env", "GH_CLIENT_SECRET"], Token);

        Assert.NotEqual(0, code);
        var body = api.Calls.Single(static c => c.Method == "POST").Body;
        Assert.Contains(ClientSecretCanary, body, StringComparison.Ordinal);
        Assert.DoesNotContain("GH_CLIENT_SECRET", body, StringComparison.Ordinal);
        AssertNeitherPrinted();
        Assert.Contains("https://provider.example.test/authorize", _error.ToString(), StringComparison.Ordinal);   // the consent URL is for the owner; it holds no secret
    }

    [Fact]
    public async Task Both_values_in_one_command_stay_out_of_what_a_server_refusal_prints()
    {
        var api = new RoutedApi().OnRaw("/sindri/connections", HttpStatusCode.Conflict, """{"title":"SND-CONN-010","detail":"A connection with that name already exists"}""");

        var code = await Verbs(api).Run(["connection", "add", "acme:key", "--kind", "rest", "--key-env", "ACME_KEY", "--client-secret-env", "GH_CLIENT_SECRET"], Token);

        Assert.NotEqual(0, code);
        AssertNeitherPrinted();
    }

    [Fact]
    public async Task An_unset_variable_is_named_in_the_error_sends_nothing_and_prints_no_value()
    {
        var api = new RoutedApi();

        var code = await Verbs(api).Run(["connection", "add", "acme:key", "--kind", "rest", "--key-env", "NOT_IN_THIS_SHELL"], Token);

        Assert.Equal(2, code);
        Assert.Contains("NOT_IN_THIS_SHELL", _error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(api.Calls, static c => c.Method == "POST");
        AssertNeitherPrinted();
    }

    [Fact]
    public async Task Signin_posts_to_the_connection_prints_the_consent_url_and_waits_for_a_newer_refresh()
    {
        var before = Added("linear:work", "oauth").Connection with { Status = "needs-sign-in", StatusReason = "refresh failed", LastRefreshedAt = DateTimeOffset.UnixEpoch };
        var after = before with { Status = "connected", StatusReason = null, LastRefreshedAt = DateTimeOffset.UnixEpoch.AddHours(1) };
        var api = new RoutedApi()
            .OnPost("/sindri/connections/linear%3Awork/signin", HttpStatusCode.OK, new AddConnectionResponse(before, "https://provider.example.test/authorize?state=x"))
            .On("/sindri/connections/linear%3Awork", HttpStatusCode.OK, new GetConnectionResponse(after));

        var code = await Verbs(api).Run(["connection", "signin", "linear:work"], Token);

        Assert.Equal(0, code);
        Assert.Contains("https://provider.example.test/authorize", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("connected", _out.ToString(), StringComparison.Ordinal);
    }
}
