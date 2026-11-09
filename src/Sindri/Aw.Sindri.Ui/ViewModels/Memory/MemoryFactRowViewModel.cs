using Aw.Sindri.Ui.Api.Memory;

namespace Aw.Sindri.Ui.ViewModels.Memory;

/// <summary>One fact of a search: its text, tags, where it came from, when it was refreshed and expires, and its score. It goes away only after the API confirms a forget.</summary>
internal sealed class MemoryFactRowViewModel
{
    private readonly Func<string, CancellationToken, Task> _forget;

    /// <summary>The row over a served fact.</summary>
    /// <param name="fact">The fact.</param>
    /// <param name="clock">The clock expiry is read against.</param>
    /// <param name="forgetFact">Sends the forget for one fact id.</param>
    /// <param name="forgetTag">Sends the forget for one tag.</param>
    internal MemoryFactRowViewModel(MemoryFactView fact, TimeProvider clock, Func<string, CancellationToken, Task> forgetFact, Func<string, CancellationToken, Task> forgetTag)
    {
        ArgumentNullException.ThrowIfNull(fact);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(forgetFact);
        ArgumentNullException.ThrowIfNull(forgetTag);
        (Fact, _forget) = (fact, forgetFact);
        Tags = [.. fact.Tags.Select(t => new MemoryTagViewModel(t, ct => forgetTag(t, ct)))];
        var provenance = new (string Kind, string? Value)[] { ("workflow", fact.Provenance.Workflow), ("run", fact.Provenance.Run), ("item", fact.Provenance.Item), ("session", fact.Provenance.Session) }
            .Where(p => !string.IsNullOrEmpty(p.Value))
            .Select(p => (p.Kind, p.Value ?? string.Empty));
        ProvenanceText = Labels.MemoryProvenanceLine(provenance);
        RefreshedText = Labels.MemoryRefreshed(fact.RefreshedAt);
        ExpiresText = fact.ExpiresAt is { } at ? Labels.MemoryExpires(at, at <= clock.GetUtcNow()) : string.Empty;
        ScoreText = fact.Score is { } s ? Labels.MemoryScore(s) : string.Empty;
    }

    /// <summary>The served fact.</summary>
    internal MemoryFactView Fact { get; }

    /// <summary>The fact's id.</summary>
    public string Key => Fact.Key;

    /// <summary>The fact's text.</summary>
    public string Text => Fact.Text;

    /// <summary>The tags, each with its forget action.</summary>
    public IReadOnlyList<MemoryTagViewModel> Tags { get; }

    /// <summary>True when the fact has tags.</summary>
    public bool HasTags => Tags.Count > 0;

    /// <summary>"workflow w · run r"; empty when nothing says where it came from.</summary>
    public string ProvenanceText { get; }

    /// <summary>True when the fact says where it came from.</summary>
    public bool HasProvenance => ProvenanceText.Length > 0;

    /// <summary>"Refreshed 2026-01-01".</summary>
    public string RefreshedText { get; }

    /// <summary>"Expires 2026-02-01" or "Expired 2026-01-01"; empty when it never expires.</summary>
    public string ExpiresText { get; }

    /// <summary>True when the fact has an expiry.</summary>
    public bool HasExpiry => ExpiresText.Length > 0;

    /// <summary>"Score 0.87"; empty outside a search.</summary>
    public string ScoreText { get; }

    /// <summary>True when the fact has a score.</summary>
    public bool HasScore => ScoreText.Length > 0;

    /// <summary>False while a forget of this fact's store is in flight.</summary>
    public bool CanForget { get; private set; } = true;

    /// <summary>The forget action's words.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string ForgetText => Labels.MemoryForgetFact;
#pragma warning restore CA1822, MA0041

    /// <summary>The forget action's accessible name: it says which fact.</summary>
    public string ForgetName => Labels.MemoryForgetFact + ": " + Text;

    /// <summary>The row's accessible name.</summary>
    public string Name => string.Join(", ", new[] { Text, Tags.Count > 0 ? string.Join(' ', Tags.Select(t => t.Tag)) : string.Empty, ProvenanceText, RefreshedText, ExpiresText, ScoreText }.Where(s => s.Length > 0));

    /// <summary>Sends the forget for this fact.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task Forget(CancellationToken cancellationToken) => _forget(Key, cancellationToken);
}
