using Aw.Context.Core.Engine;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Host.Models;

/// <summary>
/// Sindri's built-in codex name (gpt-6-luna, gpt-5.5) when codex's own list ('codex debug models') does not hold it (issue 3389): the step runs codex's own default model instead, with no
/// model flag, and its name is <see cref="Name"/>. A name the profile or the request wrote is never changed: the doctor warns about that one. A list that could not be read changes nothing.
/// </summary>
internal static class CodexDefault
{
    /// <summary>The name a step carries when it runs codex's own default: what the doctor line, the tries and the Jobs page show.</summary>
    internal const string Name = "default";

    /// <summary>The route with each built-in codex step that <paramref name="listed"/> lacks turned into codex's own default.</summary>
    /// <param name="route">The route.</param>
    /// <param name="listed">Codex's list; empty when it could not be read.</param>
    /// <returns>The route to run.</returns>
    internal static IReadOnlyList<RoutedModel> Apply(IReadOnlyList<RoutedModel> route, IReadOnlyList<ModelFacts> listed) =>
        listed.Count == 0 || !route.Any(BuiltIn) ? route : [.. route.Select(s => BuiltIn(s) && !listed.Any(m => string.Equals(m.Slug, s.Model, StringComparison.Ordinal)) ? s with { Model = Name } : s)];

    /// <summary>Whether a route has a built-in codex step, so codex's list is worth reading.</summary>
    /// <param name="route">The route.</param>
    /// <returns>True when one step is a built-in codex name.</returns>
    internal static bool Needed(IReadOnlyList<RoutedModel> route) => route.Any(BuiltIn);

    /// <summary>Whether a step runs codex's own default.</summary>
    /// <param name="step">The step.</param>
    /// <returns>True for a built-in codex step <see cref="Apply"/> turned into the default.</returns>
    internal static bool Is(RoutedModel step) => BuiltIn(step) && string.Equals(step.Model, Name, StringComparison.Ordinal);

    private static bool BuiltIn(RoutedModel step) => step.Harness == ModelHarness.Codex && step.Source == Context.Core.Engine.ModelSource.Builtin;
}
