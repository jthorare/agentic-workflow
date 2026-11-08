using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Takes a launch's final usage sample (engine P8 D9) in a scope of its own: the sampler is scoped and the closing is not.</summary>
/// <param name="launch">The launch.</param>
/// <param name="cancellationToken">Cancels the sample.</param>
/// <returns>A task that ends when the sample is taken.</returns>
internal delegate ValueTask UsageFlush(LaunchId launch, CancellationToken cancellationToken);
