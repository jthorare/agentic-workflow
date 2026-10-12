namespace Aw.Judge.Core.Providers;

/// <summary>Content-class routing: for each class, the providers to try in order (spec F2).</summary>
/// <param name="Classes">The provider names per class; a class absent here has no providers.</param>
public sealed record ChainSpec(IReadOnlyDictionary<ContentClass, IReadOnlyList<ProviderName>> Classes)
{
    /// <summary>
    /// The default chain: jev, then claude-cli, then rules on every text class; image goes to claude-cli then rules (jev is
    /// text-only). Every class ends with rules, so a rule fallback can always decide.
    /// </summary>
    public static ChainSpec Default { get; } = Build([AgentCliName.ClaudeCli], jev: true);

    /// <summary>The runtime chain: text classes get jev (unless disabled), the agent CLIs in order, then rules; image the agent CLIs then rules.</summary>
    /// <param name="agentClis">The installed agent CLIs, in priority order.</param>
    /// <param name="jev">Whether jev is on.</param>
    /// <returns>The chain.</returns>
    public static ChainSpec Build(IReadOnlyList<AgentCliName> agentClis, bool jev)
    {
        ArgumentNullException.ThrowIfNull(agentClis);
        var clis = agentClis.Select(AgentCliNames.Provider).ToList();
        List<ProviderName> text = [.. jev ? [ProviderName.Jev] : Array.Empty<ProviderName>(), .. clis, ProviderName.Rules];
        var classes = ContentClasses.Text.ToDictionary(cls => cls, IReadOnlyList<ProviderName> (_) => text);
        classes[ContentClass.Image] = [.. clis, ProviderName.Rules];
        return new ChainSpec(classes);
    }

    /// <summary>The providers for a class, in order.</summary>
    /// <param name="contentClass">The class.</param>
    /// <returns>The names; empty when the class isn't routed.</returns>
    public IReadOnlyList<ProviderName> For(ContentClass contentClass) =>
        Classes.TryGetValue(contentClass, out var names) ? names : [];

    /// <summary>Limits the chain to an allowlist (--providers); rules is local and deterministic, so it is always kept.</summary>
    /// <param name="allowed">The allowed providers.</param>
    /// <returns>The restricted chain.</returns>
    public ChainSpec Restrict(IReadOnlyCollection<ProviderName> allowed)
    {
        ArgumentNullException.ThrowIfNull(allowed);
        var keep = new HashSet<ProviderName>(allowed) { ProviderName.Rules };
        return new ChainSpec(Classes.ToDictionary(
            pair => pair.Key,
            IReadOnlyList<ProviderName> (pair) => [.. pair.Value.Where(keep.Contains)]));
    }
}
