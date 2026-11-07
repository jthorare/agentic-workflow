using Aw.Context.Core.Linear;
using Aw.Context.Infrastructure.Linear;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Linear;

/// <summary>Where the write key stands (Linear hub spec 2026-11-04 D3): what 'sindri doctor' says, worked out from the settings and the resolved pointers, with nothing sent to Linear.</summary>
[Trait("Lane", "unit")]
public sealed class LinearWriteKeyStatusTests : IDisposable
{
    private const string Read = "keychain:linear/read";
    private const string Write = "keychain:linear/write";
    private readonly Dictionary<string, string?> _secrets = new(StringComparer.Ordinal) { [Read] = "read-secret", [Write] = "write-secret", ["env:FIXTURE_WRITE"] = "fixture-write-secret" };
    private readonly NeverCalledHandler _handler = new();
    private readonly HttpClient _http;
    private readonly LinearWriteClient _client;

    public LinearWriteKeyStatusTests()
    {
        _http = new HttpClient(_handler);
        _client = new LinearWriteClient(new LinearSourceParts(_http, Resolve, new FakeTimeProvider(), new CapturingLogger<LinearClient>()));
    }

    public void Dispose() => _http.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string? Resolve(string target) => _secrets.TryGetValue(target, out var secret) ? secret : throw new InvalidOperationException("the keychain is locked");

    private static LinearSettings Settings(string? writeToken, string token = Read, string? url = null) => LinearSettings.Create(url, token, ["ENG"], writeToken).Value;

    private async Task<LinearWriteKeyStatus> Status(LinearSettings settings)
    {
        var status = await _client.KeyStatus(settings, Ct);
        Assert.Equal(0, _handler.Calls);
        return status;
    }

    [Fact]
    public async Task No_write_token_is_not_set() => Assert.IsType<LinearWriteKeyNotSet>(await Status(Settings(writeToken: null)));

    [Fact]
    public async Task A_write_token_that_is_the_read_token_is_the_same_key_without_resolving_anything()
    {
        _secrets.Clear();

        _ = Assert.IsType<LinearWriteKeySameAsReadKey>(await Status(Settings(Read)));
    }

    [Fact]
    public async Task Two_pointers_that_resolve_to_the_same_secret_are_the_same_key()
    {
        _secrets[Write] = _secrets[Read];

        _ = Assert.IsType<LinearWriteKeySameAsReadKey>(await Status(Settings(Write)));
    }

    [Fact]
    public async Task A_keychain_pointer_that_resolves_to_its_own_secret_resolves() => Assert.IsType<LinearWriteKeyResolves>(await Status(Settings(Write)));

    [Fact]
    public async Task A_write_key_that_resolves_is_fine_even_when_the_read_key_does_not()
    {
        _secrets[Read] = null;

        _ = Assert.IsType<LinearWriteKeyResolves>(await Status(Settings(Write)));
    }

    [Fact]
    public async Task An_env_pointer_on_a_loopback_fixture_resolves() =>
        Assert.IsType<LinearWriteKeyResolves>(await Status(Settings("env:FIXTURE_WRITE", token: "env:FIXTURE_READ", url: "http://127.0.0.1:7777/graphql")));

    [Theory]
    [InlineData("env:FIXTURE_WRITE")]
    [InlineData("file:/run/secrets/linear-write")]
    public async Task An_env_or_file_pointer_on_linears_own_host_is_refused_because_agent_sessions_can_read_it(string target)
    {
        var refused = Assert.IsType<LinearWriteKeyRefused>(await Status(Settings(target)));

        Assert.Contains("keychain or 1Password", refused.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_pointer_that_resolves_to_nothing_is_refused_naming_its_scheme_and_doctor_but_not_the_pointer()
    {
        _secrets[Write] = null;

        var refused = Assert.IsType<LinearWriteKeyRefused>(await Status(Settings(Write)));

        Assert.Contains("keychain:", refused.Reason, StringComparison.Ordinal);
        Assert.Contains("sindri doctor", refused.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("linear/write", refused.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_pointer_whose_resolution_throws_is_refused()
    {
        _ = _secrets.Remove(Write);

        _ = Assert.IsType<LinearWriteKeyRefused>(await Status(Settings(Write)));
    }

    [Theory]
    [InlineData("lin qz9secret")]
    [InlineData("lin\nqz9secret")]
    public async Task A_secret_with_whitespace_or_control_characters_is_refused_and_never_shown(string secret)
    {
        _secrets[Write] = secret;

        var refused = Assert.IsType<LinearWriteKeyRefused>(await Status(Settings(Write)));

        Assert.DoesNotContain("qz9secret", refused.Reason, StringComparison.Ordinal);
    }

    private sealed class NeverCalledHandler : HttpMessageHandler
    {
        private int _calls;

        public int Calls => _calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref _calls);
            throw new InvalidOperationException("The key status must send nothing to Linear.");
        }
    }
}
