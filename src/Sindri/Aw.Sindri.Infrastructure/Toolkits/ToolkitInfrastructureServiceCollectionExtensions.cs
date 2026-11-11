using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Toolkits;
using Aw.Sindri.Infrastructure.Processes;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Infrastructure.Toolkits;

/// <summary>Registers the toolkit adapters (agent-first PR 16): the store, the folder reader, the build runner and the supervisor.</summary>
internal static class ToolkitInfrastructureServiceCollectionExtensions
{
    /// <summary>Adds the toolkit adapters. The supervisor is a singleton: when the container is disposed on a serve shutdown it stops every toolkit process.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (the generator's paths); null reads none.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddToolkitInfrastructure(this IServiceCollection services, Func<string, string?>? environment = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddSingleton<IToolkitStore, SqliteToolkitStore>()
            .AddSingleton<IToolkitWorkspace, FileToolkitWorkspace>()
            .AddSingleton<IToolkitFolders>(sp => new FileToolkitFolders(sp.GetRequiredService<ISindriHome>().Path, environment ?? (static _ => null), new ProcessToolRunner()))
            .AddSingleton<IToolkitCommands>(static _ => new ProcessToolkitCommands(new ProcessToolRunner()))
            .AddSingleton<IToolkitProcessLauncher, ToolkitProcess>()
            .AddSingleton(static sp => new ToolkitSupervisor(sp.GetRequiredService<IToolkitProcessLauncher>(), sp.GetRequiredService<IToolkitStore>(), sp.GetRequiredService<TimeProvider>()))
            .AddSingleton<IToolkitRuntime>(static sp => sp.GetRequiredService<ToolkitSupervisor>())
            .AddSingleton<RunningToolkits>()
            .AddSingleton<IRunningToolkits>(static sp => sp.GetRequiredService<RunningToolkits>());   // what the MCP endpoint and call_tool resolve (agent-first PR 16); the workflow registry falls back to NoToolkits when absent
    }
}
