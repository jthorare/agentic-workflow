using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.LinearConnection;

/// <summary>
/// (Also the save's one way to the live profile, to keep its constructor within the dependency limit.) Stops a pasted key from being sent to a Linear url nobody approved: the key checks and the keychain pick read the live `context.sources.linear.url`, so a hand-edited url that waits in Review changes would
/// receive the key before the save could ever fail to approve it. The routes that send a pasted key ask here first; a live url that differs from the latest approved profile's (or no approval at all) refuses the call.
/// </summary>
/// <param name="live">The live profile.</param>
/// <param name="approved">The latest approved profile.</param>
internal sealed class LinearUrlGuard(IProfileSource live, IApprovedProfile approved)
{
    /// <summary>The words of the refusal; the key was not sent anywhere.</summary>
    internal const string Waiting = "The Linear url has a change waiting for approval; your key wasn't sent (fix: approve or undo it in Review changes, then save again)";

    /// <summary>Checks the live Linear url against the approved one.</summary>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>Null when the urls match; SND-LINEAR-006 when they differ or nothing was approved; the error when a profile can't be read.</returns>
    public async ValueTask<Error?> Pending(CancellationToken cancellationToken)
    {
        var current = await live.Load(cancellationToken).ConfigureAwait(false);
        var latest = await approved.Latest(cancellationToken).ConfigureAwait(false);
        if (current.IsError || latest.IsError)
        {
            return current.IsError ? current.Error : latest.Error;
        }

        return latest.Value is { } previous && string.Equals(Url(current.Value.Profile), Url(previous.Profile), StringComparison.Ordinal)
            ? null
            : SindriErrors.LinearBadValue.With(Waiting);
    }

    /// <summary>Loads the live profile, for the hash a save answers with.</summary>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The live profile, or the error loading it gives.</returns>
    public ValueTask<Result<LoadedProfile>> Live(CancellationToken cancellationToken) => live.Load(cancellationToken);

    private static string? Url(SindriProfile profile) =>
        profile.Context is { ValueKind: JsonValueKind.Object } context
            && context.TryGetProperty("sources", out var sources) && sources.ValueKind == JsonValueKind.Object
            && sources.TryGetProperty("linear", out var linear) && linear.ValueKind == JsonValueKind.Object
            && linear.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String
                ? url.GetString()
                : null;
}
