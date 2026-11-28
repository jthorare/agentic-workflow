namespace Aw.Sindri.Host.Commands;

/// <summary>The verbs that run before any container, and the container every other verb runs in (#3727).</summary>
internal static partial class CommandTable
{
    /// <summary>
    /// 'serve' (the control plane builds its own), 'sandbox explain|show' (they read only the plan file, never the state
    /// directory, the ledger or telemetry: sandbox spec 3.6) and the usage ('help', '--help', '-h'); null for every other verb.
    /// </summary>
    /// <param name="args">The command line.</param>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="clock">The clock.</param>
    /// <returns>The exit code, or null when the verb needs a container.</returns>
    private static async Task<int?> BeforeAnyContainer(string[] args, Func<string, string?> environment, TimeProvider clock)
    {
        switch (args)
        {
            case ["serve"]:
                return await ServeCommand.Run(environment, clock, Console.Error).ConfigureAwait(false);   // in the foreground; the lease keeps a second one out
            case ["sandbox", "explain", .. var explain]:
                return await SandboxExplainVerb.Run(explain, environment, Console.Out, Console.Error, CancellationToken.None).ConfigureAwait(false);
            case ["sandbox", "show", .. var show]:
                return await SandboxShowVerb.Run(show, environment, Console.Out, Console.Error, CancellationToken.None).ConfigureAwait(false);
            case [_] when CliCompositions.For(args) == CliComposition.None:
                await Console.Out.WriteAsync(Usage.Text + "\n").ConfigureAwait(false);
                return 0;
            default:
                return null;
        }
    }

    /// <summary>The registrations <paramref name="args"/> runs with: the client composition for a verb that only calls the API, else the full one.</summary>
    /// <param name="args">The command line, after the skills flag is applied.</param>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="clock">The clock.</param>
    /// <returns>The service collection, not built yet.</returns>
    private static IServiceCollection Container(string[] args, Func<string, string?> environment, TimeProvider clock) =>
        CliCompositions.For(args) == CliComposition.Client
            ? Composition.ComposeClient(new ServiceCollection(), environment, clock)   // no stores, skills folder or engines
            : Composition.Compose(new ServiceCollection(), environment, clock);
}
