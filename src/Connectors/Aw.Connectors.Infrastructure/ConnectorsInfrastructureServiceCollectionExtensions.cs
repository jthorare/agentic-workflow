using Aw.Connectors.Core.GitHub;
using Aw.Connectors.Core.Slack;
using Aw.Connectors.Infrastructure.GitHub;
using Aw.Connectors.Infrastructure.Slack;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Connectors.Infrastructure;

/// <summary>Registers Aw.Connectors.Infrastructure's adapters and settings. A second technology for a port gets its own AddConnectors&lt;Tech&gt;Infrastructure method.</summary>
public static class ConnectorsInfrastructureServiceCollectionExtensions
{
    /// <summary>Adds the Connectors adapters and their settings.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddConnectorsInfrastructure(this IServiceCollection services, Func<string, string?> environment)
        => services.AddSingleton(SettingsFromEnvironment.Read(environment));

    /// <summary>Adds the GitHub reader: the client behind <see cref="IGitHubReader"/>, on a handler that follows no redirect and refuses private hosts unless the profile marks one as intended.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="host">An Enterprise API host, or null for api.github.com.</param>
    /// <param name="allowPrivateHost">True when the profile marks a private, loopback or link-local host as intended.</param>
    /// <param name="token">The already resolved read-only token, or null to call unauthenticated.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddGitHubReader(this IServiceCollection services, string? host, bool allowPrivateHost, string? token)
    {
        var settings = new GitHubClientSettings { Host = host, AllowPrivateHost = allowPrivateHost, Token = token };
        return services.AddSingleton<IGitHubReader>(_ => new GitHubClient(
            new HttpClient(GitHubHttp.CreateHandler(settings, new SystemHostResolver()), disposeHandler: true),
            settings,
            new SystemHostResolver(),
            TimeProvider.System));
    }

    /// <summary>Adds the Slack reader: the client behind <see cref="ISlackReader"/>, on a handler that follows no redirect, talking only to slack.com, and refusing any channel id not in <paramref name="channelIds"/> before a request is sent.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="token">The already resolved bot token.</param>
    /// <param name="channelIds">The channel ids the profile lists.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSlackReader(this IServiceCollection services, string? token, IReadOnlyCollection<string> channelIds)
    {
        ArgumentNullException.ThrowIfNull(channelIds);
        var settings = new SlackClientSettings { Token = token, Channels = [.. channelIds] };
        return services.AddSingleton<ISlackReader>(_ => new SlackClient(new HttpClient(SlackHttp.CreateHandler(), disposeHandler: true), settings, TimeProvider.System));
    }
}
