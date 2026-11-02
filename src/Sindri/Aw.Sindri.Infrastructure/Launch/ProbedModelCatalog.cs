using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The models each agent offers, read when asked (model-pick spec §3): the Claude catalog entry, the Codex reader over 'codex debug models', and nothing for any other agent.</summary>
/// <param name="read">One agent's list, or null when it can't be read.</param>
internal sealed class ProbedModelCatalog(Func<AgentId, CancellationToken, ValueTask<IReadOnlyList<ModelFacts>?>> read) : IModelCatalog
{
    /// <summary>Runs a program and gives its trimmed stdout, or null when it failed or timed out.</summary>
    /// <param name="program">The program.</param>
    /// <param name="arguments">Its arguments.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The output or null.</returns>
    internal delegate ValueTask<string?> Run(string program, IReadOnlyList<string> arguments, CancellationToken cancellationToken);

    /// <summary>The readers of this box: Claude Code's catalog entry and Codex's command.</summary>
    /// <param name="run">How a program is run (5 s at most).</param>
    /// <returns>The reader for <see cref="ProbedModelCatalog"/>.</returns>
    internal static Func<AgentId, CancellationToken, ValueTask<IReadOnlyList<ModelFacts>?>> Readers(Run run) => async (agent, cancellationToken) => agent.Value switch
    {
        "claude-code" => ClaudeModels.Catalog,
        "codex" => await run("codex", ["debug", "models"], cancellationToken).ConfigureAwait(false) is { } json ? CodexModels.Read(json) : null,
        _ => null,
    };

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ModelFacts>> ModelsOf(AgentId agent, CancellationToken cancellationToken) =>
        await read(agent, cancellationToken).ConfigureAwait(false) ?? [];
}
