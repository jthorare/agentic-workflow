namespace Aw.Sindri.Core.Setup;

/// <summary>Onboarding after approval as the setup service's tick sees it (setup-once spec §5.1): each repo the latest approved profile names gets 'repo.onboarded' once per approval.</summary>
public interface IRepoOnboardingTick
{
    /// <summary>Onboards each repo of the latest approved profile that is not yet recorded for its hash.</summary>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task that ends when every repo was tried; a failure is tried again on the next tick.</returns>
    public ValueTask Ensure(CancellationToken cancellationToken);
}
