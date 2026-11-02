using Aw.Kernel;

namespace Aw.Sindri.Host.Commands;

/// <summary>'sindri services …' and 'sindri jobs …' (API P6 unit H): the service and job verbs, with the job follower they share.</summary>
internal static class JobCommands
{
    /// <summary>Runs the verb family <paramref name="args"/> names.</summary>
    /// <param name="args">The command line, 'services …', 'jobs …' or 'index embed …'.</param>
    /// <param name="api">The API client and where the verbs write.</param>
    /// <param name="ensureRunning">Starts the control plane (true: this verb may launch it) or names why not.</param>
    /// <param name="clock">Times the polling fallback and the wait for a cancel.</param>
    /// <param name="interrupt">The first and the second Ctrl-C.</param>
    /// <returns>The exit code.</returns>
    internal static async Task<int> Run(string[] args, (ControlPlaneClient Client, Output Output) api, Func<bool, CancellationToken, Task<Error?>> ensureRunning, TimeProvider clock,
        (CancellationToken Interrupt, CancellationToken Again) interrupt)
    {
        if (args is ["jobs", .. var jobs])
        {
            return await new JobVerbs(api.Client, api.Output, ensureRunning).Run(jobs, interrupt.Interrupt).ConfigureAwait(false);
        }

        var services = args is ["index", "embed", .. var embed] ? IndexEmbedArguments.Services(embed) : args[1..];   // 'index embed' is 'services … index-embed'
        return services.IsError
            ? await api.Output.Error(services.Error).ConfigureAwait(false)
            : await new ServiceVerbs(api.Client, api.Output, new JobFollower(api.Client, api.Output, clock), ensureRunning).Run(services.Value, interrupt.Interrupt, interrupt.Again).ConfigureAwait(false);
    }
}
