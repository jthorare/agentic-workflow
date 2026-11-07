using System.Globalization;
using Aw.Context.Core;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>The read key's answers: SND-CONTEXT-071 for a refused key, 010 for a rate limit or a 5xx, 073 for a missing entity and 072 for any other error.</summary>
internal sealed class LinearReadErrors : LinearErrorSet
{
    /// <inheritdoc />
    public override Error KeyRefused(string address, string fix) =>
        ContextErrors.LinearKeyRefused.With($"Linear at {address} refused the key: it is missing, wrong or revoked, or can't read. {fix}");

    /// <inheritdoc />
    public override Error RateLimited(string address, string? resetHeader) => Unavailable(address, "is rate limited, retry later");

    /// <inheritdoc />
    public override Error ServerFailed(string address, string operation, int status) =>
        Unavailable(address, string.Create(CultureInfo.InvariantCulture, $"answered HTTP {status} on {operation}"));

    /// <inheritdoc />
    public override Error NotFound(string address) => ContextErrors.LinearRecordNotFound.With($"Linear at {address} has no such record.");

    /// <inheritdoc />
    public override Error Other(string address, IReadOnlyList<LinearGraphQlError> errors) =>
        ContextErrors.LinearUnexpectedReply.With(
            $"Linear at {address} answered with GraphQL errors ({string.Join("; ", errors.Take(2).Select(e => Shown(e.Message)))}); the adapter's queries (D18) may need updating.");

    private static string Shown(string text)
    {
        var line = new string([.. text.Select(c => char.IsControl(c) ? ' ' : c)]);
        return line.Length <= 120 ? line : line[..120] + "…";
    }

    private static Error Unavailable(string address, string what) => ContextErrors.SourceUnavailable.With($"Linear at {address} {what}.");
}
