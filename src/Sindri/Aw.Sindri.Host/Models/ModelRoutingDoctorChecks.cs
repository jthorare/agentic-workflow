using Aw.Context.Core.Engine;
using Aw.Context.Core.Models;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Host.Models;

/// <summary>
/// The doctor's 'context:models' row (context model routing spec §8.4): what will run (one line per distinct route) when ok, and a warning with its fixes for a listed harness that is not installed, a list of one harness
/// and a Codex model that Codex's own list does not hold. Claude has no list command and Cursor's reader is not here yet, so neither is checked.
/// </summary>
/// <param name="models">'context.models' of the approved profile.</param>
/// <param name="catalog">The models each agent offers (Codex's list).</param>
/// <param name="installed">The harnesses found on PATH, in the default order.</param>
internal sealed class ModelRoutingDoctorChecks(IApprovedModels models, IModelCatalog catalog, Func<IReadOnlyList<ModelHarness>> installed) : IDoctorChecks
{
    private const string Name = "context:models";
    private static readonly AgentId _codex = AgentId.From("codex");

    private static readonly Dictionary<ModelKind, ModelPurpose> _purposes = new()
    {
        [ModelKind.Labeling] = ModelPurpose.Labeling,
        [ModelKind.ConceptNaming] = ModelPurpose.Concepts,
        [ModelKind.DecisionExtraction] = ModelPurpose.DecisionExtraction,
        [ModelKind.Adjudication] = ModelPurpose.Adjudication,
        [ModelKind.MapCleanup] = ModelPurpose.Cleanup,
        [ModelKind.Rerank] = ModelPurpose.Rerank,
    };

    public async ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        if (!await models.IsApproved(cancellationToken).ConfigureAwait(false))
        {
            return [new DoctorCheck(Name, CheckState.Ok, "not configured (no profile is approved)", Fix: null)];   // the defaults fill in only once a profile is approved; PATH is not read
        }

        var settings = await models.Current(cancellationToken).ConfigureAwait(false);
        var have = installed();
        if (have.Count == 0)
        {
            return [new DoctorCheck(Name, CheckState.Warn, "no agent CLI (claude-code, codex or cursor) is on PATH, so no model call can run", "install one of them and sign it in")];
        }

        var problems = new List<(string Detail, string Fix)>();
        ListProblems(settings, have, problems);
        var routes = Routes(settings, have);
        await CodexProblems(routes, problems, cancellationToken).ConfigureAwait(false);
        if (problems.Count == 0)
        {
            return [new DoctorCheck(Name, CheckState.Ok, Lines(routes), Fix: null)];
        }

        var distinct = problems.Distinct().ToList();
        return [new DoctorCheck(Name, CheckState.Warn, string.Join("; ", distinct.Select(static p => p.Detail).Distinct(StringComparer.Ordinal)), string.Join("; ", distinct.Select(static p => p.Fix)))];
    }

    // Every list the profile wrote: the default's and each kind's.
    private static void ListProblems(ModelsSettings settings, IReadOnlyList<ModelHarness> have, List<(string Detail, string Fix)> problems)
    {
        foreach (var (who, path, harnesses) in Lists(settings))
        {
            problems.AddRange(harnesses.Where(h => !have.Contains(h)).Select(h => (ModelHarnesses.Name(h) + " is not installed", "remove it from " + path)));
            if (harnesses.Count == 1)
            {
                problems.Add(($"{who} lists one harness, so it has no fallback", "add a second harness to " + path));
            }
        }
    }

    private static IEnumerable<(string Who, string Path, IReadOnlyList<ModelHarness> Harnesses)> Lists(ModelsSettings settings)
    {
        if (settings.Default.Harnesses is { } all)
        {
            yield return ("default", "context.models.default.harnesses", all);
        }

        foreach (var kind in ModelKinds.All)
        {
            if (settings.Entry(kind).Harnesses is { } own)
            {
                yield return (ModelKinds.Key(kind), $"context.models.{ModelKinds.Key(kind)}.harnesses", own);
            }
        }
    }

    private static List<(ModelKind Kind, IReadOnlyList<RoutedModel> Route)> Routes(ModelsSettings settings, IReadOnlyList<ModelHarness> have) =>
        [.. ModelKinds.All.Select(kind => (kind, ModelRouting.Resolve(settings, new ModelRequest(_purposes[kind], Prompt: string.Empty, MaxTokens: 0, TimeSpan.Zero), have)))];

    // One line per distinct route, kinds in the profile docs' order: "labeling, mapCleanup: claude-code haiku, then codex gpt-6-luna".
    private static string Lines(List<(ModelKind Kind, IReadOnlyList<RoutedModel> Route)> routes) =>
        string.Join("; ", routes.GroupBy(static r => Words(r.Route), StringComparer.Ordinal).Select(g => $"{string.Join(", ", g.Select(static r => ModelKinds.Key(r.Kind)))}: {g.Key}"));

    private static string Words(IReadOnlyList<RoutedModel> route) =>
        route.Count == 0 ? "no harness installed" : string.Join(", then ", route.Select(static s => ModelHarnesses.Name(s.Harness) + " " + s.Model));

    private async ValueTask CodexProblems(List<(ModelKind Kind, IReadOnlyList<RoutedModel> Route)> routes, List<(string Detail, string Fix)> problems, CancellationToken cancellationToken)
    {
        var steps = routes.SelectMany(static r => r.Route.Where(static s => s.Harness == ModelHarness.Codex).Select(s => (r.Kind, Step: s))).ToList();
        if (steps.Count == 0)
        {
            return;
        }

        var listed = await catalog.ModelsOf(_codex, cancellationToken).ConfigureAwait(false);
        if (listed.Count == 0)
        {
            return;   // the list could not be read: nothing is said about the names (never an invented list)
        }

        foreach (var (kind, step) in steps.Where(s => !listed.Any(m => string.Equals(m.Slug, s.Step.Model, StringComparison.Ordinal))))
        {
            problems.Add(($"codex model {step.Model} is not in codex's model list", $"pick one of 'codex debug models' for {KeyOf(kind, step.Source)}"));
        }
    }

    private static string KeyOf(ModelKind kind, Aw.Context.Core.Engine.ModelSource source) => source switch
    {
        Aw.Context.Core.Engine.ModelSource.KindModels => $"context.models.{ModelKinds.Key(kind)}.models.codex",
        Aw.Context.Core.Engine.ModelSource.DefaultModels => "context.models.default.models.codex",
        Aw.Context.Core.Engine.ModelSource.Request or Aw.Context.Core.Engine.ModelSource.Names or Aw.Context.Core.Engine.ModelSource.Builtin => "context.models.names",
        _ => "context.models.names",
    };
}
