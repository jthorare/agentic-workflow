namespace Aw.Judge.Core.Providers;

/// <summary>A provider's answer: <see cref="ProviderDecided"/>, <see cref="ProviderUnavailable"/> or <see cref="ProviderErrored"/>.</summary>
/// <param name="ReasonCode">Why, e.g. 'model', 'no-api-key', 'timeout' or 'exit-1'.</param>
public abstract record ProviderResult(string ReasonCode)
{
    /// <summary>
    /// What a best-effort image call dropped on the way to this answer, in words ('image 1 is 200x120, under Jev's 280 px minimum; asked jev as text');
    /// null when nothing was dropped. A provider that sets it has run the call without those images, so the rest of the chain runs as text too.
    /// </summary>
    public IReadOnlyList<string>? ImageNotes { get; init; }
}
