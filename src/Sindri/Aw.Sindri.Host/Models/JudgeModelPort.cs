using System.Text.Json.Nodes;
using Aw.Context.Core;
using Aw.Context.Core.Models;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Kernel;

namespace Aw.Sindri.Host.Models;

/// <summary>
/// The context engine's model port (context spec §4) over Judge's provider catalog: a request goes to the owner's own agent CLIs
/// (Claude Code, Codex, Cursor) in the catalog's order, never to the remote 'jev' API, because the prompt names files of the owner's
/// repo. The first provider that answers wins; none answering is an unavailable source, which a map build reports as a degraded
/// 'labels' pass rather than a failure.
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

            var result = await provider.Decide(question, input: null, request.Budget, cancellationToken).ConfigureAwait(false);
            if (result is ProviderDecided { Extra: { } extra } && extra[TextKey] is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0)
            {
                return new ModelAnswer(text, Tokens(request.Prompt), Tokens(text));
            }
        }

        return ContextErrors.SourceUnavailable.With("no agent CLI (claude, codex, cursor-agent) answered the request (fix: log in to one of them; labeling can be turned off with 'context.labeling.enabled')");
    }

    private static int Tokens(string text) => (text.Length + 3) / 4;
}
