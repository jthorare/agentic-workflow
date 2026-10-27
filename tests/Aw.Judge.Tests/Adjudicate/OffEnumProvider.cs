using System.Text.Json.Nodes;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Tests.Adjudicate;

/// <summary>A claude-cli provider that breaks the provider contract on purpose: it answers a decision outside the question's outputs as decided.</summary>
internal sealed class OffEnumProvider : IModelProvider
{
    public int Calls { get; private set; }

    public ProviderName Name => ProviderName.ClaudeCli;

    public IReadOnlySet<ContentClass> Classes { get; } = new HashSet<ContentClass>(ContentClasses.All);

    public ValueTask<ProviderResult> Decide(QuestionRef question, JsonNode? input, TimeSpan budget, CancellationToken cancellationToken)
    {
        Calls++;
        return ValueTask.FromResult<ProviderResult>(new ProviderDecided("not-an-output", 1, "model"));
    }
}
