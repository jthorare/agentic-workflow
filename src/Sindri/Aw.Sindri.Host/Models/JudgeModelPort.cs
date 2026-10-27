using System.Text.Json.Nodes;
using Aw.Context.Core;
using Aw.Context.Core.Models;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Kernel;

namespace Aw.Sindri.Host.Models;

/// <summary>
/// The context engine's model port (context spec §4) over Judge's provider catalog: a request goes to the owner's own agent CLIs
/// (Claude Code, Codex, Cursor) in the catalog's order, never to the remote 'jev' API, because the prompt carries the owner's
/// repo text (the concept scan's labelled files). The owner relaxed that rule for one question only (q123, 2026-10-26): the
/// judge's 'model-pick', which sends a step's brief and pack to Jev after the pack filters (denyPaths, secret file names, a secret
/// scan) and the size cap; this port does not ask it, and what it asks still never reaches Jev. The first provider that answers
/// wins; none answering is SND-CONTEXT-042 (labeling unavailable, naming the fix), which a map build reports as a degraded note
/// and a sync as queued decisions, never a failure. A request's model is the CLI's --model flag: the CLI's entry in
/// <see cref="ModelRequest.Models"/>, else <see cref="ModelRequest.Model"/>, else the CLI's own default.
/// </summary>
/// <param name="catalog">Finds the provider lineup (resolved on the first request).</param>
internal sealed class JudgeModelPort(Func<IProviderCatalog> catalog) : IModelPort
{
    private const string Decision = "answered";
    private const string TextKey = "text";

    private static readonly ProviderName[] _agentClis = [.. AgentCliNames.DefaultOrder.Select(AgentCliNames.Provider)];

    public async ValueTask<Result<ModelAnswer>> Ask(ModelRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var lineup = catalog().Lineup(config: null, _agentClis, new NoRulesProvider());
        var question = new QuestionRef
        {
            Name = "context-" + request.Purpose.ToString().ToLowerInvariant(),
            Outputs = [Decision],
            Prompt = request.Prompt,
            ContentClass = ContentClass.Code,
            ExtraProperties = new Dictionary<string, JsonObject>(StringComparer.Ordinal) { [TextKey] = new() { ["type"] = "string" } },
            Criteria = new Dictionary<string, string>(StringComparer.Ordinal) { [Decision] = "reply with the whole answer in the text field" },
        };
        foreach (var name in lineup.Chain.For(ContentClass.Code))
        {
            var provider = lineup.Providers.FirstOrDefault(p => p.Name == name && p.Name != ProviderName.Rules);
            if (provider is null)
            {
                continue;
            }

            var asked = ModelFor(request, name) is { } model ? question with { Model = model } : question;
            var result = await provider.Decide(asked, input: null, request.Budget, cancellationToken).ConfigureAwait(false);
            if (result is ProviderDecided { Extra: { } extra } && extra[TextKey] is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0)
            {
                return new ModelAnswer(text, Tokens(request.Prompt), Tokens(text), asked.Model);
            }
        }

        return ContextErrors.LabelingUnavailable.With("no agent CLI (claude, codex, cursor-agent) answered the request");
    }

    // The CLI's own entry in the request's models, else the request's one model, else none (the CLI's default).
    private static string? ModelFor(ModelRequest request, ProviderName provider) =>
        request.Models is { } models && models.TryGetValue(CliKey(provider), out var named) ? named : request.Model;

    // The key the request's model names use for an agent CLI.
    private static string CliKey(ProviderName provider) => provider switch
    {
        ProviderName.ClaudeCli => "claude",
        ProviderName.CodexCli => "codex",
        ProviderName.CursorCli => "cursor",
        ProviderName.Rules or ProviderName.Jev => string.Empty,
        _ => string.Empty,
    };

    private static int Tokens(string text) => (text.Length + 3) / 4;
}
