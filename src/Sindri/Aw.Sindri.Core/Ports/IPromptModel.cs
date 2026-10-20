using Aw.Kernel;

namespace Aw.Sindri.Core.Ports;

/// <summary>
/// The model a prompt eval asks (SC-E): Sindri's own port, so Features holds no Judge type; the Host adapts Judge's provider catalog
/// to it, as it does for the context engine's model port. The providers it may ask come from the repo's profile.
/// </summary>
public interface IPromptModel
{
    /// <summary>The providers the repo's profile allows a prompt eval to send text to; empty means nothing may leave the box.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The provider names, in the order they are asked; never 'rules', which sends nothing.</returns>
    public ValueTask<IReadOnlyList<string>> AllowedProviders(CancellationToken cancellationToken);

    /// <summary>Sends one prompt to the first allowed provider that answers.</summary>
    /// <param name="ask">The prompt, the role whose model answers and the time budget.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The answer, or SND-RECIPE-015 when no allowed provider answered.</returns>
    public ValueTask<Result<PromptAnswer>> Ask(PromptAsk ask, CancellationToken cancellationToken);
}
