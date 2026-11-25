namespace Aw.Sindri.Host.Commands;

/// <summary>
/// Picks the container for a command line before anything is built (#3727). A verb is Client only when every arm it can
/// reach in CommandTable.Dispatch takes the API client, the probe, the output, the start command or the clock, and never
/// the service provider; anything else, and any verb not named here, is Full.
/// </summary>
internal static class CliCompositions
{
    /// <summary>The container <paramref name="args"/> runs in.</summary>
    /// <param name="args">The command line, after the skills flag is applied.</param>
    /// <returns>The composition.</returns>
    internal static CliComposition For(string[] args) => args switch
    {
        ["help" or "--help" or "-h"] => CliComposition.None,
        [] or ["--headless"] => CliComposition.Client,
        ["status" or "stop" or "views" or "handoff" or "open" or "ledger" or "answer", ..] => CliComposition.Client,
        ["queue" or "items" or "item" or "project" or "projects" or "connection" or "connections" or "memory" or "memories" or "resources" or "toolkit" or "toolkits", ..] => CliComposition.Client,
        ["request" or "requests" or "decisions" or "decide" or "needs-you" or "services" or "jobs", ..] => CliComposition.Client,
        ["agents" or "skill" or "skills" or "code-index" or "library", ..] => CliComposition.Client,
        ["index", "connect", ..] => CliComposition.Full,   // IndexConnectVerbs reads the profile writers
        ["index", ..] => CliComposition.Client,
        ["setup", .. var rest] when Array.Exists(rest, static a => string.Equals(a, "--apply", StringComparison.Ordinal)) => CliComposition.Client,
        ["repo", "add" or "onboard", ..] => CliComposition.Client,
        ["launch", "probe" or "picks", ..] => CliComposition.Client,
        ["context", .. var rest] => Context(rest),
        ["wf", "validate", .. var rest] when !rest.Contains("--slug", StringComparer.Ordinal) => CliComposition.Full,   // a graph file is checked in process
        ["wf", ..] => CliComposition.Client,
        _ => CliComposition.Full,
    };

    /// <summary>'context …': pin, page set|remove and eval (but eval backtest) run in process; every other context verb calls the API.</summary>
    private static CliComposition Context(string[] rest) => rest switch
    {
        ["pin", ..] or ["page", "set" or "remove", ..] => CliComposition.Full,
        ["eval", "backtest", ..] => CliComposition.Client,
        ["eval", ..] => CliComposition.Full,
        _ => CliComposition.Client,
    };
}
