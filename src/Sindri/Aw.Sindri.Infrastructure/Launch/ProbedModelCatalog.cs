using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The models each agent offers, read when asked (model-pick spec §3): the Claude catalog entry, the Codex reader over 'codex debug models', the Cursor reader over 'cursor-agent --list-models' (a documented list when cursor-agent is installed but the list is unavailable; nothing when it is not installed), and nothing for any other agent.</summary>
/// <param name="read">One agent's list, or null when it can't be read.</param>
internal sealed class ProbedModelCatalog(Func<AgentId, CancellationToken, ValueTask<IReadOnlyList<ModelFacts>?>> read) : IModelCatalog
{
    /// <summary>Runs a program and gives its trimmed stdout, or null when it failed or timed out.</summary>
    /// <param name="program">The program.</param>
    /// <param name="arguments">Its arguments.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The output or null.</returns>
    internal delegate ValueTask<string?> Run(string program, IReadOnlyList<string> arguments, CancellationToken cancellationToken);

    /// <summary>The readers of this box: Claude Code's catalog entry, Codex's command and Cursor's command.</summary>
    /// <param name="run">How a program is run (5 s at most).</param>
    /// <param name="installed">Whether a program is on PATH; null looks at this box's PATH.</param>
    /// <returns>The reader for <see cref="ProbedModelCatalog"/>.</returns>
    internal static Func<AgentId, CancellationToken, ValueTask<IReadOnlyList<ModelFacts>?>> Readers(Run run, Func<string, bool>? installed = null) => async (agent, cancellationToken) => agent.Value switch
    {
        "claude-code" => ClaudeModels.Catalog,
        "codex" => await run("codex", ["debug", "models"], cancellationToken).ConfigureAwait(false) is { } json ? CodexModels.Read(json) : null,
        "cursor" when (installed ?? CursorOnPath)("cursor-agent") => CursorModels.Read(await run("cursor-agent", ["--list-models"], cancellationToken).ConfigureAwait(false)),
        _ => null,
    };

    private static bool CursorOnPath(string program) => Notifications.NotifierCommand.OnPath(program) is not null;

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ModelFacts>> ModelsOf(AgentId agent, CancellationToken cancellationToken) =>
        await read(agent, cancellationToken).ConfigureAwait(false) ?? [];
}
