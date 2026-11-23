using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Features.ConnectAccount;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.ConnectAccount;

/// <summary>ConnectAccount for GitHub (first-run spec decisions 13 and 14, §7 row A8): a gh login connects GitHub through `command:gh auth token`, and no token is copied or stored.</summary>
public sealed class ConnectAccountTests
{
    private const string GhCommand = "command:gh auth token --hostname github.com";

    private readonly ConnectAccountRig _rig = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<Result<ConnectAccountResponse>> ConnectGitHubAsOwner()
    {
        using var entered = _rig.Slice.Callers.Enter(A.Caller().Human().Build());
        return await _rig.Service().Handle(new ConnectAccountRequest("github"), Token);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Gh_login_sets_review_credential_to_the_gh_command()
    {
        _rig.Gh.SignedInAs = "ada";
        using var entered = _rig.Slice.Callers.Enter(A.Caller().Human().Build());

        var result = await _rig.Service().Handle(new ConnectAccountRequest("github"), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Assert.Equal(new ConnectAccountResponse("github", "connected"), result.Value);
        Assert.Contains($"text review.credential={GhCommand}", _rig.Writer.Calls, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_github_connection_the_index_source_and_review_credential_read_one_entry()
    {
        _rig.Gh.SignedInAs = "ada";

        _ = (await ConnectGitHubAsOwner()).Value;

        var row = Assert.Single(_rig.Connections.Rows);
        Assert.Equal((GitHubCliCredential.ConnectionName, ConnectionKind.Cli, "github", ConnectionStatus.Connected, null), (row.Name, row.Kind, row.Preset, row.Status, row.SecretPointer));
        Assert.Equal((GhCommand, "ada"), (row.Config["credential"], row.Config["login"]));
        var commands = new MapCommands(GhCommand, "gho-from-gh");
        var indexSource = new ConnectionCredentials(_rig.Connections, new FakeConnectionSecrets(), new FakeSecretWriter(), commands);
        Assert.Equal("gho-from-gh", indexSource.TokenFor("github", profilePointer: null));   // the github index source and the connection: one entry
        Assert.Contains($"text review.credential={row.Config["credential"]}", _rig.Writer.Calls, StringComparer.Ordinal);   // review.credential: the same entry
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_first_connect_on_a_box_with_no_profile_creates_the_profile_then_writes_the_credential()
    {
        _rig.Gh.SignedInAs = "ada";
        _rig.Writer.MissingUntilInitialized = _rig.Initializer;

        var result = await ConnectGitHubAsOwner();

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Assert.Equal(1, _rig.Initializer.Created);
        Assert.Equal([$"text review.credential={GhCommand}", $"text review.credential={GhCommand}", "save", "remove setup.skipAccounts", "save"], _rig.Writer.Calls, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Connecting_github_clears_skip_accounts()
    {
        _rig.Gh.SignedInAs = "ada";

        _ = (await ConnectGitHubAsOwner()).Value;

        Assert.Equal([$"text review.credential={GhCommand}", "save", "remove setup.skipAccounts", "save"], _rig.Writer.Calls, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_gh_login_github_is_refused_and_nothing_is_written()
    {
        var result = await ConnectGitHubAsOwner();

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
        Assert.Contains("gh auth login", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal((0, 0), (_rig.Writer.Calls.Count, _rig.Connections.Rows.Count));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_agent_session_is_refused_and_nothing_is_written()
    {
        _rig.Gh.SignedInAs = "ada";
        using var entered = _rig.Slice.Callers.Enter(A.Caller().Launched().Build());

        var result = await _rig.Service().Handle(new ConnectAccountRequest("github"), Token);

        Assert.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
        Assert.Equal((0, 0), (_rig.Writer.Calls.Count, _rig.Connections.Rows.Count));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_provider_that_is_not_an_account_is_invalid_input()
    {
        using var entered = _rig.Slice.Callers.Enter(A.Caller().Human().Build());

        var result = await _rig.Service().Handle(new ConnectAccountRequest("gitlab"), Token);

        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
    }

    private sealed class MapCommands(string credential, string token) : ICommandCredentials
    {
        public string? Token(string asked) => string.Equals(asked, credential, StringComparison.Ordinal) ? token : null;
    }
}
