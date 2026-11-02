using System.Text.Json;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>What a Step input fills from (spec §2). The context engine's phase adds what retrieval needs.</summary>
/// <param name="Input">The Step input's id.</param>
/// <param name="Step">The Step it fills for.</param>
/// <param name="Arguments">Its 'with:' arguments, resolved to values.</param>
/// <param name="Facts">The instance's facts.</param>
/// <param name="Repo">The repo the instance's item belongs to (from the ledger, never from a caller); null when it has none.</param>
/// <param name="Scope">The repo a session caller is scoped to when the fill is a pull on its behalf; null in the run's own fill.</param>
/// <param name="Instance">The workflow instance address the fill is for (issue 1005: the scoping graph is remembered under it); null when the caller has none.</param>
public sealed record StepInputContext(
    BlockId Input,
    BlockId Step,
    IReadOnlyDictionary<string, JsonElement> Arguments,
    IReadOnlyDictionary<FactKey, JsonElement> Facts,
    RepoName? Repo = null,
    RepoName? Scope = null,
    string? Instance = null);
