using System.Text.Json;
using Aw.Judge.Core.Config;
using Aw.Judge.Core.Ports;
using Aw.Judge.Infrastructure;

namespace Aw.Sindri.Host.Models;

/// <summary>
/// Judge's provider catalog and batch classifier, built on first use in a container of its own: Judge's infrastructure registers its own stores, settings and
/// id source, none of which may join Sindri's container, so Sindri takes only those out of it (context spec §4, §6.7).
/// </summary>
/// <param name="environment">Reads an environment variable (AW_PROVIDER, PATH).</param>
/// <param name="clock">The clock.</param>
/// <param name="json">The JSON options Judge's container is given.</param>
internal sealed class JudgeCatalog(Func<string, string?> environment, TimeProvider clock, JsonSerializerOptions json)
{
    private readonly Lazy<ServiceProvider> _provider = new(() => new ServiceCollection()
        .AddSingleton(clock)
        .AddSingleton(json)
        .AddJudgeInfrastructure(environment)
        .BuildServiceProvider());

    /// <summary>The catalog, building Judge's container on the first call.</summary>
    /// <returns>The provider catalog.</returns>
    internal IProviderCatalog Get() => _provider.Value.GetRequiredService<IProviderCatalog>();

    /// <summary>The judge's process runner, which the key probe asks the keychain through.</summary>
    /// <returns>The runner.</returns>
    internal IProcessRunner ProcessRunner() => _provider.Value.GetRequiredService<IProcessRunner>();

    /// <summary>Jev's batch classifier, the one the prompt sorter and trait-check ask through.</summary>
    /// <returns>The classifier.</returns>
    internal IBatchClassifier Classifier() => _provider.Value.GetRequiredService<IBatchClassifier>();

    /// <summary>The 'providers' block of the judge's config.json ('inputCapChars', and 'jev: false' which leaves Jev out), read each time so an edit to it counts.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The block, or null for the defaults (no file, no block, or a config that doesn't read).</returns>
    internal async ValueTask<ProvidersConfig?> Providers(CancellationToken cancellationToken)
    {
        await using var scope = _provider.Value.CreateAsyncScope();
        var read = ConfigReading.From(await scope.ServiceProvider.GetRequiredService<IJudgeConfigStore>().Load(cancellationToken).ConfigureAwait(false));
        return read.IsSuccess ? read.Value.Config.Providers : null;
    }
}
