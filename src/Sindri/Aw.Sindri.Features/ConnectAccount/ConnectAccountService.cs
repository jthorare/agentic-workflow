using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Features.ConnectAccount;

/// <summary>
/// Connects one account of the accounts question (first-run spec decisions 13 and 14). The owner's: a caller without the per-start human token gets SND-AUTH-002. GitHub with a gh login points every GitHub reader at one entry,
/// `command:gh auth token` (decision 14): `review.credential`, and the `github:gh` connection the `github` index source reads first (`ConnectionCredentials`). No token is copied or stored. The first write creates the profile, and
/// `setup.skipAccounts` is cleared. Linear, Slack and GitHub without a gh login are later slices of the accounts step.
/// </summary>
/// <param name="callers">The caller.</param>
/// <param name="gh">The gh login on this box.</param>
/// <param name="profile">Writes `review.credential` and clears `setup.skipAccounts`.</param>
/// <param name="connections">The Connections page's rows.</param>
/// <param name="time">Stamps the connection row.</param>
internal sealed class ConnectAccountService(ICallerContextAccessor callers, IGitHubCliLogin gh, AccountProfileWrites profile, IConnectionStore connections, TimeProvider time)
    : IService<ConnectAccountRequest, ConnectAccountResponse>
{
    private const string GitHub = "github";

    public async ValueTask<Result<ConnectAccountResponse>> Handle(ConnectAccountRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (OwnerCaller.Refusal(callers, "Connecting an account") is { } refused)
        {
            return refused;
        }

        if (!string.Equals(request.Provider, GitHub, StringComparison.Ordinal))
        {
            if (string.Equals(request.Provider, "linear", StringComparison.Ordinal) || string.Equals(request.Provider, "slack", StringComparison.Ordinal))
            {
                throw new NotSupportedException($"Connecting {request.Provider} is not built yet (first-run spec decisions 15 to 17)");
            }

            return SindriErrors.InvalidInput.With($"'{request.Provider}' is not an account Sindri connects: github, linear or slack");
        }

        if (await gh.Login(cancellationToken).ConfigureAwait(false) is not { } login)
        {
            return SindriErrors.InvalidInput.With("No gh login on this box, and Sign in to GitHub isn't built yet (fix: run gh auth login, then connect GitHub again)");
        }

        var written = await profile.GitHub(GitHubCliCredential.Command, cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            return written.Error;
        }

        var stored = await connections.Upsert(await Row(login, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        return stored.IsError ? stored.Error : new ConnectAccountResponse(GitHub, "connected");
    }

    // The github:gh row: a command-line credential with no secret behind it; a row already there keeps its creation stamp.
    private async ValueTask<Connection> Row(string login, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var existing = await connections.Get(GitHubCliCredential.ConnectionName, cancellationToken).ConfigureAwait(false);
        var config = new Dictionary<string, string>(StringComparer.Ordinal) { [GitHubCliCredential.ConfigKey] = GitHubCliCredential.Command, ["login"] = login };
        return new Connection(
            GitHubCliCredential.ConnectionName,
            ConnectionKind.Cli,
            GitHub,
            config,
            SecretPointer: null,
            ConnectionStatus.Connected,
            StatusReason: null,
            existing.IsSuccess ? existing.Value.CreatedBy : callers.Current?.Actor ?? "unknown",
            existing.IsSuccess ? existing.Value.CreatedAt : now,
            now,
            LastRefreshedAt: null);
    }
}
