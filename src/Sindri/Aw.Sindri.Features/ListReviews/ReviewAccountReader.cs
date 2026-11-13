using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Features.ListReviews;

/// <summary>
/// Who the Reviews tab reads as (#2499): the GitHub connection's state and /user login, the connect action when there is no usable connection, and the forge credential's login to suggest when neither the profile nor the connection knows one.
/// </summary>
/// <param name="account">The GitHub connection.</param>
/// <param name="forge">The forge port, for the credential's login.</param>
internal sealed class ReviewAccountReader(IGitHubAccount account, IPullRequests forge)
{
    /// <summary>Reads the account and decides the connect action and the login.</summary>
    /// <param name="profile">The approved profile, or null.</param>
    /// <param name="lastCode">The github source's last error code, or null.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The decision, or the store's error.</returns>
    internal async ValueTask<Result<ReviewIdentity>> Identity(SindriProfile? profile, string? lastCode, CancellationToken cancellationToken)
    {
        var current = await account.Current(cancellationToken).ConfigureAwait(false);
        if (current.IsError)
        {
            return current.Error;
        }

        var connect = ReviewConnectRules.For(current.Value, HasProfileToken(profile?.Context), lastCode);
        var typed = profile?.Review?.Login;
        return new ReviewIdentity(connect, string.IsNullOrWhiteSpace(typed) ? current.Value.Login : typed, current.Value.Connection, current.Value.Miss);
    }

    /// <summary>The forge credential's own login, for the login prompt to suggest.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The login, or null.</returns>
    internal async ValueTask<string?> Suggested(CancellationToken cancellationToken)
    {
        var suggested = await forge.Self(cancellationToken).ConfigureAwait(false);
        return suggested.IsError ? null : suggested.Value;
    }

    // why: the profile's token pointer lets the feed read without a connection, so a missing connection is not a reason to ask for one
    private static bool HasProfileToken(JsonElement? context) =>
        context is { ValueKind: JsonValueKind.Object } root
        && root.TryGetProperty("sources", out var sources) && sources.ValueKind == JsonValueKind.Object
        && sources.TryGetProperty("github", out var github) && github.ValueKind == JsonValueKind.Object
        && github.TryGetProperty("token", out var token) && token.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(token.GetString());
}
