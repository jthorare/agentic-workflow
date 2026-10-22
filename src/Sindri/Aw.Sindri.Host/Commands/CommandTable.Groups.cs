namespace Aw.Sindri.Host.Commands;

/// <summary>The verb groups Dispatch hands a whole family to.</summary>
internal static partial class CommandTable
{
    /// <summary>'sindri profile validate|init|approve …'.</summary>
    private static async Task<int> Profile(string[] args, Verbs verbs, Func<(string? Hash, bool NoStart), Verbs> approving, Output output, CancellationToken ct) => args switch
    {
        ["profile", "validate", .. var rest] when Json(rest) is { } json => await verbs.Validate(json, ct).ConfigureAwait(false),
        ["profile", "init", .. var rest] when Json(rest) is { } json => await verbs.InitProfile(json, ct).ConfigureAwait(false),
        ["profile", "approve", .. var rest] when Approval(rest) is { } a => await approving(a).Approve(a.Hash, Interactive(), Console.In, ct).ConfigureAwait(false),
        _ => await UnknownVerb(args, output).ConfigureAwait(false),
    };

    /// <summary>'sindri agents|setup|code-index …': the read-only verbs that only call the control plane.</summary>
    private static async Task<int> ReadVerbs(string[] args, ControlPlaneClient client, Output output, CancellationToken ct) => args switch
    {
        ["agents", .. var rest] => await new AgentsVerbs(client, output).Run(rest, ct).ConfigureAwait(false),
        ["setup", .. var rest] => await new SetupVerbs(client, output).Run(rest, ct).ConfigureAwait(false),
        ["code-index", .. var rest] => await new CodeIndexVerbs(client, output).Run(rest, ct).ConfigureAwait(false),
        _ => await UnknownVerb(args, output).ConfigureAwait(false),
    };
}
