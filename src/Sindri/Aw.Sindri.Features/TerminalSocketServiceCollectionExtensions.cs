using Aw.Kernel.Services;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.TerminalSocket;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the terminal WebSocket's slice (agents spec §15.8, A8).</summary>
internal static class TerminalSocketServiceCollectionExtensions
{
    /// <summary>Adds the pane lookup the terminal route asks before the upgrade, so SND-AGENT-015 and -016 answer as HTTP.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddTerminalSocket(this IServiceCollection services) => services
        .AddScoped<IValidator<FindTerminalPaneRequest>, AcceptAllValidator<FindTerminalPaneRequest>>()
        .AddQuery<FindTerminalPaneRequest, FindTerminalPaneResponse, FindTerminalPaneService>();
}
