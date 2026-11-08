using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections.OAuth;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Infrastructure.Connections;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.Connections;

/// <summary>KeychainConnectionSecrets over the real writer and resolver and a fake secret-tool or security: one base64url JSON secret, nothing in an argument list.</summary>
[Lane(TestLane.Unit)]
public sealed partial class KeychainConnectionSecretsTests
{
    private const string Access = "acc-SECRET-0123456789";
    private const string Refresh = "ref-SECRET-9876543210";
    private static readonly TokenSet _tokens = new(Access, Refresh, new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero));

    [GeneratedRegex("^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Base64Url { get; }

    private static ToolRun Ok(string output = "") => new(ToolOutcome.Exited, 0, output);

    private static (KeychainConnectionSecrets Secrets, FakeToolRunner Runner, Func<string?> Stored) Linux()
    {
        string? stored = null;
        FakeToolRunner? runner = null;
        runner = new FakeToolRunner((_, args) => args[0] switch
        {
            "store" => Remember(),
            "lookup" => stored is null ? new ToolRun(ToolOutcome.Exited, 1, string.Empty) : Ok(stored),
            "clear" => Forget(),
            _ => ToolRun.Missing,
        });
        ToolRun Remember()
        {
            stored = runner!.Inputs[^1];   // why: the fake keeps each call's standard input
            return Ok();
        }

        ToolRun Forget()
        {
            stored = null;
            return Ok();
        }

        var secrets = new KeychainConnectionSecrets(
            new KeychainSecretWriter(runner, new FakeSindriHome(Path.Combine(Path.GetTempPath(), "aw-no-such-home")), KeychainPlatform.Linux),
            new PointerSecrets(static _ => null, runner, KeychainPlatform.Linux),
            runner,
            KeychainPlatform.Linux);
        return (secrets, runner, () => stored);
    }

    [Fact]
    public void The_tokens_are_stored_as_ONE_base64url_json_secret_and_read_back()
    {
        var (secrets, _, stored) = Linux();
        var name = ConnectionSamples.Name("linear", "work");

        var pointer = secrets.Store(name, _tokens);
        var read = secrets.Read(pointer.Value);

        Assert.True(pointer.IsSuccess);
        Assert.Equal("keychain:sindri-connection/linear.work", pointer.Value.ToString());
        Assert.Matches(Base64Url, stored()!);
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(stored()!.Replace('-', '+').Replace('_', '/').PadRight((stored()!.Length + 3) / 4 * 4, '=')));
        using var document = JsonDocument.Parse(json);
        Assert.Contains(Access, json, StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        Assert.Equal(_tokens, read.Value);
    }

    [Fact]
    public void No_token_is_in_any_argument_of_any_call()
    {
        var (secrets, runner, stored) = Linux();
        var pointer = secrets.Store(ConnectionSamples.Name(), _tokens).Value;
        _ = secrets.Read(pointer);
        _ = secrets.Delete(pointer);

        var arguments = runner.Calls.SelectMany(static c => c.Arguments).Append(pointer.ToString()).ToList();

        Assert.NotEmpty(runner.Calls);
        Assert.DoesNotContain(arguments, static a => a.Contains(Access, StringComparison.Ordinal) || a.Contains(Refresh, StringComparison.Ordinal));
        Assert.Null(stored());
    }

    [Fact]
    public void Delete_runs_secret_tool_clear_for_the_same_service_and_account_on_linux()
    {
        var (secrets, runner, _) = Linux();
        var pointer = secrets.Store(ConnectionSamples.Name("slack", "team"), _tokens).Value;

        var deleted = secrets.Delete(pointer);

        Assert.True(deleted.IsSuccess);
        var call = runner.Calls[^1];
        Assert.Equal("secret-tool", call.FileName);
        Assert.Equal(["clear", "service", "sindri-connection", "account", "slack.team"], call.Arguments);
        Assert.True(secrets.Read(pointer).IsError);
    }

    [Fact]
    public void Delete_runs_security_delete_generic_password_on_macos()
    {
        var runner = new FakeToolRunner(Ok());
        var secrets = new KeychainConnectionSecrets(new FakeSecretWriter(), new FakeSecretWriter(), runner, KeychainPlatform.MacOS);

        var deleted = secrets.Delete(SecretPointer.Parse("keychain:sindri-connection/slack.team").Value);

        Assert.True(deleted.IsSuccess);
        var call = Assert.Single(runner.Calls);
        Assert.Equal("security", call.FileName);
        Assert.Equal(["delete-generic-password", "-s", "sindri-connection", "-a", "slack.team"], call.Arguments);
    }

    [Fact]
    public void Deleting_what_is_not_stored_is_not_an_error()
    {
        var runner = new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 1, string.Empty));
        var secrets = new KeychainConnectionSecrets(new FakeSecretWriter(), new FakeSecretWriter(), runner, KeychainPlatform.Linux);

        Assert.True(secrets.Delete(SecretPointer.Parse("keychain:sindri-connection/slack.team").Value).IsSuccess);
    }

    [Fact]
    public void A_secret_store_that_fails_is_SND_CONN_006_without_the_token()
    {
        var writer = new FakeSecretWriter { Fails = true };
        var secrets = new KeychainConnectionSecrets(writer, writer, new FakeToolRunner(Ok()), KeychainPlatform.Linux);

        var stored = secrets.Store(ConnectionSamples.Name(), _tokens);

        Assert.True(stored.IsError);
        Assert.Equal(SindriErrors.ConnectionSecretUnavailable.Code, stored.Error.Code);
        Assert.DoesNotContain(Access, stored.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Refresh, stored.Error.Message, StringComparison.Ordinal);
    }
}
