namespace Aw.Sindri.Host.Commands;

internal static partial class CommandTable
{
    /// <summary>'sindri sandbox learn off|denials|check|exec|login …'; any other sandbox verb is an unknown verb.</summary>
    private static async Task<int> Sandbox(string[] args, ControlPlaneClient client, Output output, IServiceProvider services, (CancellationToken Ct, CallerHeaders Caller) run)
    {
        var ct = run.Ct;
        return args switch
        {
            ["sandbox", "learn", "off", .. var rest] => await new SandboxLearnVerbs(client, output).Run(rest, ct).ConfigureAwait(false),
            ["sandbox", "check", .. var rest] => await new SandboxCheckVerb(client, output).Run(rest, ct).ConfigureAwait(false),
            ["sandbox", "denials", .. var rest] => await new SandboxDenialsVerb(client, output, services.GetRequiredService<TimeProvider>()).Run(rest, ct).ConfigureAwait(false),
            ["sandbox", "exec", .. var rest] => await new SandboxExecVerb(output, services, run.Caller).Run(rest, ct).ConfigureAwait(false),   // human-only, and a CLI path rather than an API route
            ["sandbox", "login", .. var rest] => await new SandboxLoginVerb(output, services, run.Caller).Run(rest, ct).ConfigureAwait(false),   // human-only, and a CLI path rather than an API route
            _ => await UnknownVerb(args, output).ConfigureAwait(false),
        };
    }
}
