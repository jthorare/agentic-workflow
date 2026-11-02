using Aw.Connectors.Core;
using Aw.Connectors.Core.GitHub;
using Aw.Connectors.Core.Slack;
using Aw.Connectors.Features.PullGitHub;
using Aw.Connectors.Features.PullSlack;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Connectors.Features;

/// <summary>Registers Aw.Connectors.Features's slices: each service, its validator and its decorators (outermost first).</summary>
public static class ConnectorsFeaturesServiceCollectionExtensions
{
    /// <summary>Adds every Connectors use case as an IService.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddConnectorsFeatures(this IServiceCollection services)
    {
        _ = services
            .AddScoped<IValidator<PullGitHubRequest>, PullGitHubValidator>()
            .AddScoped<IService<PullGitHubRequest, PullGitHubResponse>>(static sp =>
                new ValidatingService<PullGitHubRequest, PullGitHubResponse>(
                    new PullGitHubService(sp.GetRequiredService<IGitHubReader>()),   // a factory registration: the reader is the caller's to add, so a host that builds readers per token registers none
                    sp.GetRequiredService<IValidator<PullGitHubRequest>>()))
            .AddScoped<IValidator<PullSlackRequest>, PullSlackValidator>()
            .AddSingleton<ISlackConnectorFactory>(static sp => new SlackConnectorFactory(sp.GetService<TimeProvider>() ?? TimeProvider.System))
            .AddScoped<IService<PullSlackRequest, PullSlackResponse>>(static sp =>
                new ValidatingService<PullSlackRequest, PullSlackResponse>(
                    new PullSlackService(sp.GetRequiredService<ISlackReader>()),   // a factory registration: the reader is the caller's to add, so a host that builds readers per token registers none
                    sp.GetRequiredService<IValidator<PullSlackRequest>>()))
            .AddSingleton<IGitHubConnectorFactory>(static sp => new GitHubConnectorFactory(sp.GetService<TimeProvider>() ?? TimeProvider.System));
        return services;
    }

    /// <summary>Adds only the GitHub connector factory: for a host that builds a reader per token and so registers no <see cref="IGitHubReader"/> (Sindri's Host), where the PullGitHub and PullSlack slices would not resolve.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddGitHubConnectorFactory(this IServiceCollection services) =>
        services.AddSingleton<IGitHubConnectorFactory>(static sp => new GitHubConnectorFactory(sp.GetService<TimeProvider>() ?? TimeProvider.System));

    /// <summary>Adds only the Slack connector factory: for a host that builds a reader per token and so registers no <see cref="ISlackReader"/> (Sindri's Host).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSlackConnectorFactory(this IServiceCollection services) =>
        services.AddSingleton<ISlackConnectorFactory>(static sp => new SlackConnectorFactory(sp.GetService<TimeProvider>() ?? TimeProvider.System));

    /// <summary>Adds the Slack connector as an <see cref="IConnector"/> over the registered <see cref="ISlackReader"/> (add the reader with AddSlackReader) and the PullSlack slice.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="settings">The checked 'context.sources.slack' entry.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSlackConnector(this IServiceCollection services, SlackSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return services.AddScoped<IConnector>(sp => new SlackConnector(
            settings,
            sp.GetRequiredService<ISlackReader>(),
            sp.GetRequiredService<IService<PullSlackRequest, PullSlackResponse>>()));
    }
}
