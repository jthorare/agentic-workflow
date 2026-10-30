using Aw.Sindri.Core;

namespace Aw.Sindri.Host.Commands;

/// <summary>The verb groups Dispatch hands a whole family to.</summary>
internal static partial class CommandTable
{
    /// <summary>'sindri profile validate|init|set|approve …'.</summary>
    private static async Task<int> Profile(string[] args, Verbs verbs, Func<(string? Hash, bool NoStart), Verbs> approving, (Output Output, bool Launched) caller, CancellationToken ct) => args switch
    {
        ["profile", "validate", .. var rest] when Json(rest) is { } json => await verbs.Validate(json, ct).ConfigureAwait(false),
        ["profile", "init", .. var rest] when Json(rest) is { } json => await verbs.InitProfile(json, ct).ConfigureAwait(false),
        ["profile", "set", .. var rest] when SetArguments(rest) is { } set => await verbs.SetProfileKey(set.Key, set.Value, set.Json, caller.Launched, ct).ConfigureAwait(false),
        ["profile", "approve", .. var rest] when Approval(rest) is { } a => await approving(a).Approve(a.Hash, ct).ConfigureAwait(false),
        _ => await UnknownVerb(args, caller.Output).ConfigureAwait(false),
    };

    /// <summary>'profile set &lt;key&gt; &lt;value&gt; [--json]', the flag first or last; the key and value are the two words that aren't it.</summary>
    private static (string Key, string Value, bool Json)? SetArguments(string[] rest) => rest switch
    {
        [var key, var value] when key != "--json" && value != "--json" => (key, value, false),
        ["--json", var key, var value] when key != "--json" && value != "--json" => (key, value, true),
        [var key, var value, "--json"] when key != "--json" && value != "--json" => (key, value, true),
        _ => null,
    };

    /// <summary>'sindri agents|setup|code-index|index …': the verbs that only call the control plane (the reads never start it; 'index sync' may).</summary>
    private static async Task<int> ReadVerbs(string[] args, (ControlPlaneClient Client, Output Output) api, (IServiceProvider Services, StartCommand Start) host, (CancellationToken Interrupt, CancellationToken Again) signals)
    {
        var (client, output) = api;
        var ct = signals.Interrupt;
        return args switch
        {
            ["agents", .. var rest] => await new AgentsVerbs(client, output).Run(rest, ct).ConfigureAwait(false),
            ["setup", .. var rest] => await new SetupVerbs(client, output).Run(rest, ct).ConfigureAwait(false),
            ["code-index", .. var rest] => await new CodeIndexVerbs(client, output).Run(rest, ct).ConfigureAwait(false),
            ["index", .. var rest] => await new IndexVerbs(client, output, new JobFollower(client, output, host.Services.GetRequiredService<TimeProvider>()), Interactive(), Console.In, host.Start.EnsureRunning)
                .Run(rest, ct, signals.Again).ConfigureAwait(false),
            _ => await UnknownVerb(args, output).ConfigureAwait(false),
        };
    }

    /// <summary>'sindri pr babysit &lt;url&gt; …': the hand start of the PR babysitter (API16).</summary>
    private static async Task<int> Pr(string[] args, IServiceProvider services, Output output, RecipeDesk desk, CancellationToken ct) => args switch
    {
        ["pr", "babysit", .. var rest] => await HandStartCommand.Babysit(rest, desk, services, output, ct).ConfigureAwait(false),
        _ => await output.Error(SindriErrors.UnknownVerb.With($"'{string.Join(' ', args)}' is not a sindri command ({HandStartCommand.BabysitUsage})")).ConfigureAwait(false),
    };
}
