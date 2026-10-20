using System.Globalization;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Features.ValidateRecipe;
using Aw.Sindri.Host;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Testing.Workflow.Registry;
using Aw.Sindri.Tests.Workflow.Capabilities;
using Aw.Sindri.Tests.Workflow.Wiring;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow.InvalidFixtures;

/// <summary>
/// Runs an invalid fixture end to end: the composition's reader and catalog, and recipe validate's service over an
/// in-memory directory that holds the fixture beside the child recipes the fixtures name (the shipped dev child for the DT-C ones). The service takes no base,
/// no registry and no profile yet (the freeze picks a base, register supplies the registry, the profile's
/// workflow.effects comes in P6), so a fixture that needs one runs the same steps the service runs (read, load the
/// same-directory children, validate) with it added.
/// </summary>
internal static class InvalidFixtureRun
{
    private const string Directory = "fixtures";

    private static readonly string _stateRoot = Path.Combine(Path.GetTempPath(), "aw-sindri-invalid-fixtures-test");

    /// <summary>Every finding's line, in the order the service returns them; a finding that isn't an error ends with its severity.</summary>
    public static async Task<IReadOnlyList<string>> Lines(string rule, FixtureSetup setup, CancellationToken cancellationToken)
    {
        await using var provider = Composition.Compose(new ServiceCollection(), name => name == "AW_STATE_DIR" ? _stateRoot : null, new FakeTimeProvider())
            .BuildServiceProvider();
        var reader = provider.GetRequiredService<IRecipeReader>();
        var file = rule + ".yaml";
        var path = Path.Combine(Directory, file);
        var files = Files(file, RecipeFixtures.Read("invalid/" + file));
        var findings = setup switch
        {
            FixtureSetup.Shipped => await Service(reader, files, provider.GetRequiredService<BlockCatalog>(), path, cancellationToken),
            FixtureSetup.Wiring => await Service(reader, files, WiringCatalog.WithTestBlocks, path, cancellationToken),
            FixtureSetup.Capability => await Service(reader, files, CapabilityRecipe.Catalog, path, cancellationToken),
            FixtureSetup.CapabilityWithBase => await WithContext(reader, files, path, CapabilityRecipe.Catalog, RecipeProfileFacts.None, Base(reader), registry: null, cancellationToken),
            FixtureSetup.EffectAllowlist => await WithContext(reader, files, path, WiringCatalog.WithTestBlocks, ClaimOnly, baseRecipe: null, registry: null, cancellationToken),
            FixtureSetup.HumanTrigger => await WithContext(reader, files, path, WiringCatalog.WithTestBlocks, MergeOnly, baseRecipe: null, registry: null, cancellationToken),
            FixtureSetup.Registry => await WithContext(reader, files, path, provider.GetRequiredService<BlockCatalog>(), RecipeProfileFacts.None, baseRecipe: null, Registry, cancellationToken),
            FixtureSetup.CapabilityWithUnapprovedBase => await WithContext(reader, files, path, CapabilityRecipe.Catalog, RecipeProfileFacts.None, Base(reader), Registry, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(setup), setup, "Not a fixture setup."),
        };
        return [.. findings.Select(Line)];
    }

    /// <summary>The base fixture, read with <paramref name="reader"/>.</summary>
    public static RecipeDocument Base(IRecipeReader reader) =>
        reader.Read(RecipeFixtures.Read("base/workflow.cap-base.yaml"), "workflow.cap-base.yaml").Document
            ?? throw new InvalidOperationException("The base fixture doesn't bind.");

    private static RecipeProfileFacts ClaimOnly { get; } = new(new HashSet<BlockId> { BlockId.From("effect.claim.ref") });

    private static RecipeProfileFacts MergeOnly { get; } = new(new HashSet<BlockId> { BlockId.From("effect.test.merge") });

    /// <summary>
    /// The registry the registry fixtures are validated against: workflow.version-taken 1 and workflow.version-behind 1
    /// and 3, each with a hash no fixture has, and the capability base registered at version 1 but never approved.
    /// </summary>
    internal static RecipeRegistryView Registry { get; } = new(
        [
            new(RecipeId.From("workflow.version-taken"), 1, RegistryData.Hash("workflow.version-taken@1")),
            new(RecipeId.From("workflow.version-behind"), 1, RegistryData.Hash("workflow.version-behind@1")),
            new(RecipeId.From("workflow.version-behind"), 3, RegistryData.Hash("workflow.version-behind@3")),
            new(RecipeId.From("workflow.cap-base"), 1, CapabilityRecipe.BaseHash),
        ],
        new Dictionary<RecipeHash, RegistryApproval>());

    private static async Task<IReadOnlyList<RecipeFinding>> Service(IRecipeReader reader, InMemoryRecipeFiles files, BlockCatalog catalog, string path, CancellationToken cancellationToken)
    {
        var service = new ValidateRecipeService(reader, files, catalog, new ApprovedRecipeProfileFacts(new InMemoryProfiles()));
        return (await service.Handle(ValidateRecipeRequest.FromPath(path), cancellationToken)).Value.Findings;
    }

    private static async Task<IReadOnlyList<RecipeFinding>> WithContext(
        IRecipeReader reader,
        InMemoryRecipeFiles files,
        string path,
        BlockCatalog catalog,
        RecipeProfileFacts profile,
        RecipeDocument? baseRecipe,
        RecipeRegistryView? registry,
        CancellationToken cancellationToken)
    {
        var reading = reader.Read((await files.Read(path, cancellationToken)).Value, Path.GetFileName(path));
        var document = reading.Document ?? throw new InvalidOperationException($"{path} doesn't bind.");
        var children = await SameDirectoryChildren.Load(path, document.Recipe, reader, files, cancellationToken);
        return [.. reading.Findings, .. RecipeValidator.Validate(document, new ValidationContext(catalog, profile, children, baseRecipe?.Recipe) { Registry = registry })];
    }

    private static string Line(RecipeFinding finding) =>
        finding.Severity == FindingSeverity.Error ? finding.ToLine() : $"{finding.ToLine()} [{finding.Severity}]";

    /// <summary>
    /// The fixture, a 'workflow.step' child that needs no inputs (RCP-TIME-002's and the capability fixtures'), and the
    /// chain workflow.depth-1 -> ... -> workflow.depth-6 that RCP-CHILD-003's workflow.depth-0 starts.
    /// </summary>
    private static InMemoryRecipeFiles Files(string file, string text)
    {
        var files = new InMemoryRecipeFiles()
            .With(Path.Combine(Directory, file), text)
            .With(Path.Combine(Directory, "workflow.step.yaml"), Leaf("workflow.step"))
            .With(Path.Combine(Directory, "workflow.dev-child-summary.yaml"), ShippedRecipes.Read("dev/workflow.dev-child-summary.yaml"));
        for (var level = 1; level < 6; level++)
        {
            _ = files.With(Path.Combine(Directory, Depth(level) + ".yaml"), Parent(Depth(level), Depth(level + 1)));
        }

        return files.With(Path.Combine(Directory, Depth(6) + ".yaml"), Leaf(Depth(6)));
    }

    private static string Depth(int level) => "workflow.depth-" + level.ToString(CultureInfo.InvariantCulture);

    private static string Leaf(string id) =>
        $"schema: 1\nid: {id}\nversion: 1\ninitial: produce\nstates:\n"
        + "  produce: { kind: active, step: step.produce, timeout: { after: 4h, then: park }, next: \"Produce the Step output.\" }\n"
        + "  done: { kind: terminal, outcome: resolved }\n"
        + "transitions:\n  - { from: produce, to: done, on: step.completed }\n";

    private static string Parent(string id, string child) =>
        $"schema: 1\nid: {id}\nversion: 1\ninitial: running\nstates:\n"
        + $"  running: {{ kind: wait, waitFor: {{ child: {{ recipe: {child}, inputs: {{}}, onParked: propagate }} }}, timeout: {{ after: 14d, then: park }}, next: \"Waiting for {{child}}.\" }}\n"
        + "  done: { kind: terminal, outcome: resolved }\n"
        + "transitions:\n  - { from: running, to: done, on: wait.satisfied }\n";
}
