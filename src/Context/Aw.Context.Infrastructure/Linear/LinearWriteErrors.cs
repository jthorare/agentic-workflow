using System.Globalization;
using System.Text.Json;
using Aw.Context.Core.Linear;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>The write key's answers (Linear hub spec 2026-11-04 §8): SND-LINEAR-004 for a refused key, 012 for a rate limit, 011 for a 5xx, 005 for a missing entity and 015 for any other error.</summary>
internal sealed class LinearWriteErrors : LinearErrorSet
{
    private const int MaxShown = 200;

    /// <inheritdoc />
    public override Error KeyRefused(string address, string fix) => LinearHubErrors.WriteKeyRefused.With($"Linear at {address} refused the write key: it is missing, wrong, revoked or read-only. {fix}");

    /// <inheritdoc />
    public override Error RateLimited(string address, string? resetHeader)
    {
        var error = LinearHubErrors.RateLimited.With($"Linear's rate limit: try again after {Reset(resetHeader)}. Nothing was changed.");
        return ResetAt(resetHeader) is { } at
            // why: the round-trip form holds only digits and -:.Z, so the string literal needs no escaping
            ? error.WithExtension("retryAfter", JsonDocument.Parse($"\"{at.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)}\"").RootElement.Clone())
            : error;
    }

    /// <inheritdoc />
    public override Error ServerFailed(string address, string operation, int status) =>
        LinearHubErrors.NoAnswer.With(string.Create(CultureInfo.InvariantCulture, $"Linear at {address} didn't confirm {operation}: it answered HTTP {status}."));

    /// <inheritdoc />
    public override Error NotFound(string address) => LinearHubErrors.IssueNotFound.With($"Linear at {address} has no such issue or comment (deleted, moved out of your teams, or not synced yet).");

    /// <inheritdoc />
    public override Error Other(string address, IReadOnlyList<LinearGraphQlError> errors)
    {
        var first = errors[0];
        return Refusal(first.Extensions?.UserPresentableMessage ?? first.Message);
    }

    /// <summary>SND-LINEAR-015 with Linear's own words (one line, control characters removed, at most 200 characters).</summary>
    /// <param name="said">What Linear said.</param>
    /// <returns>The error.</returns>
    public static Error Refusal(string said)
    {
        var line = new string([.. said.Where(static c => !char.IsControl(c))]).Trim();
        if (line.Length > MaxShown)
        {
            line = line[..MaxShown];
        }

        return LinearHubErrors.Rejected.With($"Linear said no: {line.TrimEnd('.')}. Nothing was changed.");
    }

    // The header is the time the window resets, in epoch milliseconds (UTC).
    private static DateTimeOffset? ResetAt(string? header) =>
        long.TryParse(header, NumberStyles.None, CultureInfo.InvariantCulture, out var ms) && ms is > 0 and < 253_402_300_799_999
            ? DateTimeOffset.FromUnixTimeMilliseconds(ms).ToUniversalTime()
            : null;

    private static string Reset(string? header) => ResetAt(header)?.ToString("HH:mm 'UTC'", CultureInfo.InvariantCulture) ?? "a minute";
}
