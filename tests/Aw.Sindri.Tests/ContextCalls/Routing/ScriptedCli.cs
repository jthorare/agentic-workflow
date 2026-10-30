using System.Text.Json.Nodes;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;

namespace Aw.Sindri.Tests.ContextCalls.Routing;

/// <summary>One agent CLI that answers from a script: each Decide returns the next result, the last one repeating.</summary>
internal sealed class ScriptedCli(ProviderName name, params ProviderResult[] script) : IModelProvider
{
    private int _next;

    public List<QuestionRef> Asked { get; } = [];

    public ProviderName Name => name;

    public IReadOnlySet<ContentClass> Classes { get; } = new HashSet<ContentClass> { ContentClass.Code };

    public ValueTask<ProviderResult> Decide(QuestionRef question, JsonNode? input, TimeSpan budget, CancellationToken cancellationToken)
    {
        Asked.Add(question);
        return ValueTask.FromResult(script[Math.Min(_next++, script.Length - 1)]);
    }
}
