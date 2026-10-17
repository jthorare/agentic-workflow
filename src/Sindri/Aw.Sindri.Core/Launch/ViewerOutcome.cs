namespace Aw.Sindri.Core.Launch;

/// <summary>What <see cref="ITerminalViewer.Open"/> did.</summary>
/// <param name="Kind">The outcome.</param>
/// <param name="Code">The error code for <see cref="ViewerOutcomeKind.Failed"/>, otherwise null.</param>
/// <param name="Detail">The viewer's redacted stderr for a failure, otherwise null; private to the ledger.</param>
public sealed record ViewerOutcome(ViewerOutcomeKind Kind, string? Code = null, string? Detail = null)
{
    /// <summary>A client attached in time.</summary>
    public static ViewerOutcome Opened { get; } = new(ViewerOutcomeKind.Opened);

    /// <summary>The viewer ran; no client has attached yet.</summary>
    public static ViewerOutcome Unconfirmed { get; } = new(ViewerOutcomeKind.Unconfirmed);

    /// <summary>No viewer ran.</summary>
    public static ViewerOutcome Skipped { get; } = new(ViewerOutcomeKind.Skipped);

    /// <summary>The viewer failed.</summary>
    /// <param name="code">The catalogue code ('SND-LAUNCH-001' or '-009').</param>
    /// <param name="detail">The viewer's redacted stderr, or null.</param>
    /// <returns>The outcome.</returns>
    public static ViewerOutcome Failed(string code, string? detail = null) => new(ViewerOutcomeKind.Failed, code, detail);
}
