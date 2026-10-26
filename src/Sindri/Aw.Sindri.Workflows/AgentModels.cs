using System.Text.Json;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The 'model' parameter of an agent Step (issue 966): read like 'bundle', a literal id. A profile reference is refused at launch, since the activity has no profile-path resolver for it.</summary>
internal static class AgentModels
{
    private const string Parameter = "model";

    /// <summary>Reads the Step's 'model' parameter.</summary>
    /// <param name="step">The state's Step.</param>
    /// <returns>The model it asks for.</returns>
    internal static ModelAsk Ask(StepUse step)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (!step.With.TryGetValue(Parameter, out var value))
        {
            return new ModelAsk(Literal: null, Profile: null);
        }

        var literal = value.Literal is { ValueKind: JsonValueKind.String } text && text.GetString() is { } id && !string.IsNullOrWhiteSpace(id) ? id : null;
        return new ModelAsk(literal, literal is null ? value.Profile : null);
    }

    /// <summary>Why a launch that asks for a profile key doesn't start.</summary>
    /// <param name="profile">The profile key.</param>
    /// <returns>The message, with its fix.</returns>
    internal static string Refusal(string profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return $"the Step's model is '{{ profile: {profile} }}', which an agent Step can't resolve yet (fix: write the model id in the recipe, for example model: claude-opus-5-5)";
    }
}
