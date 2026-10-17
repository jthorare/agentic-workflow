namespace Aw.Sindri.Host;

/// <summary>The headers the API reads and writes (API spec §6, §9): shared by the caller middleware, the CLI client and the tests.</summary>
internal static class SindriHeaders
{
    /// <summary>'human', or 'session' (also the default when missing).</summary>
    internal const string Caller = "X-Sindri-Caller";

    /// <summary>'1' from the CLI at an interactive terminal; honoured only with human.</summary>
    internal const string Terminal = "X-Sindri-Terminal";

    /// <summary>'cli', 'ui' or 'prover': names the actor.</summary>
    internal const string Client = "X-Sindri-Client";

    /// <summary>The repo a session caller is scoped to.</summary>
    internal const string Repo = "X-Sindri-Repo";

    /// <summary>The per-start human token.</summary>
    internal const string Token = "X-Sindri-Token";

    /// <summary>A launch's proof: authorizes 'session wrap''s heartbeat and exit calls for that launch (API spec §8.1; SND-AUTH-003 when it doesn't match an open launch).</summary>
    internal const string LaunchSecret = "X-Sindri-Launch-Secret";

    /// <summary>On a query: the ledger sequence its answer must include (read-your-writes, API spec §2.1).</summary>
    internal const string After = "X-Sindri-After";

    /// <summary>The API version every response carries.</summary>
    internal const string ApiVersion = "Sindri-Api-Version";
}
