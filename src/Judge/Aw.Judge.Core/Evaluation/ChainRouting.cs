using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Evaluation;

/// <summary>Turns a chain into the providers to try for one question.</summary>
public static class ChainRouting
{
    /// <summary>The providers for a class in the chain's order, keeping only those present that cover the class.</summary>
    /// <param name="chain">The chain.</param>
    /// <param name="contentClass">The class.</param>
    /// <param name="providers">Every available provider.</param>
    /// <returns>The candidates.</returns>
    public static IReadOnlyList<IModelProvider> ProvidersFor(ChainSpec chain, ContentClass contentClass, IReadOnlyList<IModelProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(providers);
        return [.. chain.For(contentClass)
            .Select(name => providers.FirstOrDefault(p => p.Name == name))
            .OfType<IModelProvider>()
            .Where(p => p.Classes.Contains(contentClass))];
    }

    /// <summary>
    /// The candidates for a question: <see cref="ProvidersFor"/>, then, when the question has its own provider list, that
    /// list's order with names not among the candidates dropped.
    /// </summary>
    /// <param name="chain">The chain.</param>
    /// <param name="contentClass">The question's class.</param>
    /// <param name="providers">Every available provider.</param>
    /// <param name="questionProviders">The question's own list, or null.</param>
    /// <returns>The candidates, in the order to try them.</returns>
    public static IReadOnlyList<IModelProvider> Candidates(
        ChainSpec chain, ContentClass contentClass, IReadOnlyList<IModelProvider> providers, IReadOnlyList<ProviderName>? questionProviders)
    {
        var candidates = ProvidersFor(chain, contentClass, providers);
        return questionProviders is null
            ? candidates
            : [.. questionProviders.SelectMany(name => candidates.Where(p => p.Name == name))];
    }
}
