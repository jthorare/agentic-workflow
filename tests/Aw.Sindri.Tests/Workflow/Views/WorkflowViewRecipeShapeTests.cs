using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Testing.Views;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Views;

/// <summary>
/// A recipe is declared once: every instance of one (recipe, hash) carries the same states, loops, desk flags and fact rows, a
/// fixture recipe equals its file under recipes/ where one exists, and a fixture-only recipe obeys the validator's declaration
/// rules (RCP-TYPE-002/003, a terminal state, RCP-OUT-002), so the goldens are what the engine will serve.
/// </summary>
public sealed class WorkflowViewRecipeShapeTests
{
    private sealed record Instance(string Address, InstanceRecipe Recipe, IReadOnlyList<StateCell> States, IReadOnlyList<InputPresence> Inputs, IReadOnlyList<FactRow> Facts);

    private static List<Instance> Instances() =>
    [
        .. WorkflowViewFixtures.RootList().Instances.Select(r => new Instance(r.Address.Value, r.Recipe, r.States, r.Inputs, [])),
        .. WorkflowViewFixtures.AllDetails().Select(d => new Instance(d.Address.Value, d.Recipe, d.States, [], d.Facts)),
    ];

    private static IEnumerable<IGrouping<string, Instance>> ByRecipeHash() => Instances().GroupBy(i => $"{i.Recipe.Id.Value}@{i.Recipe.Hash.Value}", StringComparer.Ordinal);

    private static string Cell(StateCell c) =>
        string.Create(CultureInfo.InvariantCulture, $"{c.State}|{c.Order}|{c.BranchOf}|desk={c.Desk}|child={c.Child}|terminal={c.Terminal}|loops={string.Join(',', c.Loops.Select(l => string.Create(CultureInfo.InvariantCulture, $"{l.With}/{l.Max}")).Order(StringComparer.Ordinal))}");

    private static string Declared(FactRow f) => $"{f.Key}|{f.Label}|{f.Type}|{f.Direction}|{f.From}";

    [Fact, Trait("Lane", "unit")]
    public void Every_instance_of_a_recipe_hash_declares_the_same_states_loops_desk_flags_and_fact_rows()
    {
        foreach (var group in ByRecipeHash())
        {
            var states = group.Select(i => string.Join(';', i.States.Select(Cell))).Distinct(StringComparer.Ordinal).ToList();
            Assert.True(states.Count == 1, $"{group.Key}: {states.Count} different state lists");
            var facts = group.Where(i => i.Facts.Count > 0).Select(i => string.Join(';', i.Facts.Select(Declared).Order(StringComparer.Ordinal))).Distinct(StringComparer.Ordinal).ToList();
            Assert.True(facts.Count <= 1, $"{group.Key}: {facts.Count} different fact-row lists");
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void A_fixture_recipe_equals_its_file_under_recipes_where_one_exists()
    {
        var files = ShippedRecipes.Recipes
            .Select(p => RecipeComposition.ReadClean(ShippedRecipes.Read(p), p))
            .ToDictionary(d => d.Recipe.Id.Value, StringComparer.Ordinal);
        var compared = 0;
        foreach (var group in ByRecipeHash().Where(g => files.ContainsKey(g.First().Recipe.Id.Value)))
        {
            var file = files[group.First().Recipe.Id.Value];
            var recipe = group.First().Recipe;
            Assert.Equal(file.Hash.Value, recipe.Hash.Value);
            Assert.Equal(file.Recipe.Version, recipe.Version);
            var order = StatePathOrder.Of(file.Recipe);
            Assert.Equal(order.States.Select(s => (s.State, s.Order, s.BranchOf)), group.First().States.Select(c => (c.State, c.Order, c.BranchOf)));
            Assert.Equal(file.Recipe.States.Where(s => s.Value.Kind == StateKind.Terminal).Select(s => s.Key).Order(StringComparer.Ordinal),
                group.First().States.Where(c => c.Terminal).Select(c => c.State).Order(StringComparer.Ordinal));
            var facts = group.SelectMany(i => i.Facts).DistinctBy(Declared, StringComparer.Ordinal).ToList();
            if (facts.Count > 0)
            {
                Assert.Equal(file.Recipe.Outputs.Select(o => (o.Key.Value, o.From)).Order(),
                    facts.Where(f => f.Direction == FactDirection.Output).Select(f => (f.Key, f.From!)).Order());
                Assert.Equal(file.Recipe.Inputs.Select(i => i.Key.Value).Order(StringComparer.Ordinal), facts.Where(f => f.Direction == FactDirection.Input).Select(f => f.Key).Order(StringComparer.Ordinal));
            }

            compared++;
        }

        Assert.True(compared >= 3, "the dev recipes and workflow.step are compared");
    }

    // The shipped catalog says which Steps are deskCapable; the engine runs those at the desk until the agent runtime (WF 12, W20).
    private static readonly BlockCatalog _catalog = FixtureCatalog.Shipped();

    private static string M(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    [Fact, Trait("Lane", "unit")]
    public void A_fixture_recipe_declaration_equals_its_file_in_every_field_the_file_carries()
    {
        var files = ShippedRecipes.Recipes
            .Select(p => RecipeComposition.ReadClean(ShippedRecipes.Read(p), p))
            .ToDictionary(d => d.Recipe.Id.Value, StringComparer.Ordinal);
        var compared = FixtureRecipes.All.Where(s => files.ContainsKey(s.Id)).ToList();
        var failures = compared.SelectMany(shape => Mismatches(shape, files[shape.Id])).ToList();
        Assert.True(failures.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, failures));
        Assert.Contains(compared, s => s.Id == "workflow.step");
        Assert.Contains(compared, s => s.Id == "workflow.dev-desk-round-trip");
        Assert.Contains(compared, s => s.Id == "workflow.dev-triage-three-state");
    }

    private static List<string> Mismatches(RecipeShape shape, RecipeDocument file)
    {
        var recipe = file.Recipe;
        var failures = new List<string>();
        if (file.Hash.Value != shape.Stamp.Hash.Value || recipe.Version != shape.Version || Urgencies.Parse(recipe.Urgency) != shape.Urgency || recipe.Budget?.Tokens != shape.BudgetTokens)
        {
            failures.Add(M($"{shape.Id}: hash, version, urgency or recipe budget differ from the file (file {file.Hash.Value} v{recipe.Version} {recipe.Urgency}, declared {shape.Stamp.Hash.Value} v{shape.Version} {shape.Urgency})"));
        }

        if (!recipe.States.Keys.Order(StringComparer.Ordinal).SequenceEqual(shape.States.Select(s => s.Id).Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            failures.Add(M($"{shape.Id}: the state ids differ from the file"));
        }

        failures.AddRange(shape.States.Where(s => recipe.States.ContainsKey(s.Id)).SelectMany(s => StateMismatches(shape.Id, s, recipe.States[s.Id])));
        failures.AddRange(LoopMismatches(shape, recipe));
        failures.AddRange(FactMismatches(shape, recipe));
        return failures;
    }

    private static List<string> StateMismatches(string id, ShapeState state, RecipeState declared)
    {
        var file = FileSignature(declared);
        var shape = M($"{state.Kind}|{state.ChildRecipe}|{state.OnParked}|{state.Desk}|{state.Block}|{state.Timeout?.After}|{state.Timeout?.Then}|{state.BudgetTokens}");
        return file == shape ? [] : [M($"{id}/{state.Id}: kind|child|onParked|desk|block|timeout|then|budget is {shape}, the file has {file} (an active deskCapable Step runs at the desk until P8: WF 12, W20)")];
    }

    private static string FileSignature(RecipeState declared)
    {
        var deskCapable = declared.Kind == StateKind.Active && declared.Step is { } step && _catalog.Find(step.Use) is { DeskCapable: true };
        var child = declared.WaitFor?.Child;
        return M($"{declared.Kind}|{child?.Recipe.Value}|{child?.OnParked}|{deskCapable}|{declared.Step?.Use.Value}|{declared.Timeout?.After.Span}|{declared.Timeout?.Then}|{declared.Budget?.Tokens}");
    }

    private static List<string> LoopMismatches(RecipeShape shape, Recipe recipe)
    {
        static string Edge(string? name, string from, string to) => M($"{name}:{from}>{to}");
        var failures = new List<string>();
        var file = StatePathOrder.Of(recipe).Loops.Select(l => Edge(l.Name, l.From, l.To)).Order(StringComparer.Ordinal);
        if (!file.SequenceEqual(shape.Loops.Select(l => Edge(l.Name, l.From, l.To)).Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            failures.Add(M($"{shape.Id}: the loop edges differ from the file"));
        }

        failures.AddRange(shape.Loops.Where(l => !recipe.Loops.TryGetValue(l.Name, out var spec) || spec.Cap != l.Max).Select(l => M($"{shape.Id}: loop {l.Name} has another cap in the file")));
        return failures;
    }

    private static List<string> FactMismatches(RecipeShape shape, Recipe recipe)
    {
        var failures = new List<string>();
        var inputs = recipe.Inputs.Select(i => M($"{i.Key.Value}|{i.Label}|{i.Type ?? FactTypes.Default}|{i.Kind}"));
        if (!inputs.SequenceEqual(shape.Inputs.Select(i => M($"{i.Key}|{i.Label}|{FactTypeWord(i.Type)}|{i.Kind}")), StringComparer.Ordinal))
        {
            failures.Add(M($"{shape.Id}: the inputs (key, label, type, kind) differ from the file: {string.Join('/', inputs)}"));
        }

        var outputs = recipe.Outputs.Select(o => M($"{o.Key.Value}|{o.Label}|{o.Type ?? FactTypes.Default}|{o.From}"));
        if (!outputs.SequenceEqual(shape.Outputs.Select(o => M($"{o.Key}|{o.Label}|{FactTypeWord(o.Type)}|{o.From}")), StringComparer.Ordinal))
        {
            failures.Add(M($"{shape.Id}: the outputs (key, label, type, from) differ from the file"));
        }

        return failures;
    }

    [Fact, Trait("Lane", "unit")]
    public void A_fixture_only_recipe_passes_the_validator_declaration_rules()
    {
        foreach (var group in ByRecipeHash())
        {
            var states = group.First().States;
            Assert.Contains(states, c => c.Terminal);
            foreach (var fact in group.SelectMany(i => i.Facts).Where(f => f.Direction == FactDirection.Output))
            {
                Assert.False(states.Single(c => c.State == fact.From).Terminal, $"{group.Key}: {fact.Key} comes from a terminal state (RCP-OUT-002)");
            }

            foreach (var input in group.SelectMany(i => i.Inputs).Where(i => i.Kind is not null))
            {
                Assert.True(SubjectKinds.IsKnown(input.Kind), $"{group.Key}: kind {input.Kind} (RCP-TYPE-002)");
                var fact = group.SelectMany(i => i.Facts).FirstOrDefault(f => f.Key == input.Key);
                if (fact is not null && SubjectKinds.PairedType(input.Kind) is { } paired)
                {
                    Assert.Equal(paired, FactTypeWord(fact.Type));
                }
            }
        }
    }

    private static string FactTypeWord(FactType type) => type switch
    {
        FactType.ScopeMap => "scope-map",
        FactType.Text or FactType.File or FactType.Item or FactType.Link or FactType.Packet or FactType.Evidence or FactType.Decision or FactType.Payload => type.ToString().ToLowerInvariant(),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "no such fact type"),
    };

    [Fact, Trait("Lane", "unit")]
    public void A_child_has_a_real_state_of_its_recipe_and_a_not_started_one_offers_open_item_only()
    {
        var stateIds = Instances().GroupBy(i => i.Recipe.Id.Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.SelectMany(i => i.States.Select(c => c.State)).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var view in WorkflowViewFixtures.AllDetails())
        {
            foreach (var group in view.Children)
            {
                foreach (var row in group.Rows.Concat(group.Workstreams.SelectMany(w => w.Rows)))
                {
                    Assert.Contains(row.State, stateIds[group.Recipe.Value]);
                    Assert.NotEqual("queued", row.State, StringComparer.Ordinal);
                    if (row.Glyph != StateGlyph.Active)
                    {
                        Assert.Equal(["OpenItem"], row.Actions.Select(a => a.Value), StringComparer.Ordinal);
                    }

                    Assert.Equal(row.Glyph == StateGlyph.Pending ? "QUEUED" : row.Chip.Word, row.Chip.Word);
                }
            }
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void A_parked_child_shows_its_park_on_the_parents_band_without_parking_the_parent()
    {
        var project = WorkflowViewFixtures.ProjectBuild();
        var row = project.Children.Single().Workstreams.SelectMany(w => w.Rows).Single(r => r.ItemId!.Value.Value == "ITM-01JD7");
        Assert.Equal(EngineOwnedKind.Parked, row.MirroredPark?.Kind);
        Assert.Null(project.EngineOwned);
        Assert.Equal("build", project.Current.Status.Header.State);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_item_instance_at_produce_round_two_of_five_has_its_parent()
    {
        var view = WorkflowViewFixtures.ItemProduce();
        Assert.Equal("ITM-01JE7/item/1/step.4", view.Address.Value);
        Assert.Equal("ITM-01JE7", view.ItemId!.Value.Value);
        Assert.Equal("ITM-01JE7/item/1", view.Parent!.Address.Value);
        Assert.Equal("produce", view.State);
        Assert.Equal((2, 5), (view.States.Single(c => c.State == "produce").Round!.N, view.States.Single(c => c.State == "produce").Round!.Max));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_goldens_cover_a_child_group_with_more_rows_and_only_a_marked_preview_misses_an_input()
    {
        // v1 serves every input required and present (StartInstance refuses a missing one); the missing-input row is a marked preview (SCR5-2, CSC5-2).
        var missing = WorkflowViewFixtures.AllSummaries().Where(r => r.Inputs.Any(i => !i.Present)).Select(r => r.Address.Value).ToList();
        Assert.NotEmpty(missing);
        Assert.All(missing, a => Assert.Contains(a, WorkflowViewFixtures.MissingInputPreviews));
        Assert.Contains(WorkflowViewFixtures.PreviewMissingInput().Instances, r => r.Inputs.Any(i => i.Required && !i.Present));
        Assert.All(WorkflowViewFixtures.RootList().Instances.Concat(WorkflowViewFixtures.OpenList().Instances), r => Assert.All(r.Inputs, i => Assert.True(i.Required && i.Present)));
        Assert.Contains(WorkflowViewFixtures.AllDetails().SelectMany(d => d.Children), g => g.More > 0);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_recipe_loop_is_served_with_its_cap()
    {
        foreach (var recipe in new[] { WorkflowViewFixtures.IncidentRecipe(), WorkflowViewFixtures.ProjectRecipe() })
        {
            var json = System.Text.Json.JsonSerializer.SerializeToElement(recipe);
            var loop = json.EnumerateObject().Single(p => string.Equals(p.Name, "loops", StringComparison.OrdinalIgnoreCase)).Value.EnumerateArray().First();
            Assert.Contains(loop.EnumerateObject(), p => string.Equals(p.Name, "cap", StringComparison.OrdinalIgnoreCase) && p.Value.GetInt32() == 3);
        }
    }

    private static System.Text.Json.JsonElement RecipeGolden(string id)
    {
        var path = Golden.SourcePath(typeof(WorkflowViewRecipeShapeTests).Assembly, $"Golden/Api/workflow-recipe/{id}.json");
        Assert.True(File.Exists(path), $"no recipe golden workflow-recipe/{id}.json");
        return System.Text.Json.JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
    }

    private static string Text(System.Text.Json.JsonElement e, string name) => e.GetProperty(name).ValueKind == System.Text.Json.JsonValueKind.Null ? "" : e.GetProperty(name).ToString();

    [Fact, Trait("Lane", "unit")]
    public void Every_recipe_an_instance_or_list_golden_names_has_a_recipe_golden_that_agrees_with_them()
    {
        var ids = Instances().Select(i => i.Recipe.Id.Value)
            .Concat(WorkflowViewFixtures.AllDetails().SelectMany(d => d.Children).Select(g => g.Recipe.Value))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        Assert.True(ids.Count >= 8, "too few recipes to be the whole fixture");
        foreach (var id in ids)
        {
            var golden = RecipeGolden(id);
            Assert.Equal(id, golden.GetProperty("id").GetString());
            var instances = Instances().Where(i => i.Recipe.Id.Value == id).ToList();
            foreach (var stamp in instances.Select(i => i.Recipe).DistinctBy(r => r.Hash.Value, StringComparer.Ordinal))
            {
                Assert.Equal(stamp.Hash.Value, golden.GetProperty("hash").GetString());
                Assert.Equal(stamp.Version, golden.GetProperty("version").GetInt32());
            }

            if (instances.Count > 0)
            {
                AgreesWithInstances(golden, instances);
            }
        }
    }

    private static void AgreesWithInstances(System.Text.Json.JsonElement golden, List<Instance> instances)
    {
        var failures = AgreementFailures(golden, instances);
        Assert.True(failures.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    private static string Pair(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? M($"{a}~{b}") : M($"{b}~{a}");

    private static string Label(System.Text.Json.JsonElement d) => Text(d, "label") is { Length: > 0 } label ? label : Text(d, "key")[5..];

    private static string TypeWord(string word) => word.Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

    private static System.Text.Json.JsonElement.ArrayEnumerator Items(System.Text.Json.JsonElement golden, string name) => golden.GetProperty(name).EnumerateArray();

    /// <summary>
    /// Everything the golden and the instance goldens both say about one recipe, compared for equality in both directions: the
    /// states, the loops as (from, to, cap, name) against the cells' loop pairs (with, max) and the recipe declaration, the declared
    /// inputs (key, label, kind, required) against every instance row's input slots, and the declared inputs and outputs (key, label,
    /// type, from) against every detail view's fact rows.
    /// </summary>
    private static List<string> AgreementFailures(System.Text.Json.JsonElement golden, List<Instance> instances)
    {
        var id = golden.GetProperty("id").GetString()!;
        var states = Items(golden, "states").Select(s => M($"{Text(s, "id")}|{Text(s, "order")}|{Text(s, "branchOf")}"));
        List<string> failures = instances[0].States.Select(c => M($"{c.State}|{c.Order}|{c.BranchOf}")).SequenceEqual(states, StringComparer.Ordinal) ? [] : [$"{id}: the states differ from the instance goldens"];
        failures.AddRange(LoopFailures(id, golden, instances[0]));
        failures.AddRange(InputFailures(id, golden, instances));
        failures.AddRange(FactFailures(id, golden, instances));
        return failures;
    }

    private static List<string> LoopFailures(string id, System.Text.Json.JsonElement golden, Instance first)
    {
        var failures = new List<string>();
        var loops = Items(golden, "loops").Select(l => M($"{Pair(Text(l, "from"), Text(l, "to"))}:{l.GetProperty("cap").GetInt32()}")).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var cells = first.States.SelectMany(c => c.Loops.Select(l => M($"{Pair(c.State, l.With)}:{l.Max}"))).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (!loops.SequenceEqual(cells, StringComparer.Ordinal))
        {
            failures.Add($"{id}: the loops (from, to, cap) are [{string.Join(' ', loops)}], the cells' loop pairs (with, max) [{string.Join(' ', cells)}]");
        }

        if (FixtureRecipes.All.SingleOrDefault(r => r.Id == id) is { } shape)
        {
            var named = Items(golden, "loops").Select(l => M($"{Text(l, "name")}:{Text(l, "from")}>{Text(l, "to")}:{l.GetProperty("cap").GetInt32()}")).Order(StringComparer.Ordinal);
            if (!named.SequenceEqual(shape.Loops.Select(l => M($"{l.Name}:{l.From}>{l.To}:{l.Max}")).Order(StringComparer.Ordinal), StringComparer.Ordinal))
            {
                failures.Add($"{id}: the loops (name, from, to, cap) differ from the recipe declaration");
            }
        }

        return failures;
    }

    private static List<string> InputFailures(string id, System.Text.Json.JsonElement golden, List<Instance> instances)
    {
        var inputs = Items(golden, "inputs").Select(i => M($"{Text(i, "key")}|{Text(i, "label")}|{Text(i, "kind")}|{i.GetProperty("required").GetBoolean()}")).ToList();
        return [.. instances.Where(i => i.Inputs.Count > 0 && !i.Inputs.Select(p => M($"{p.Key}|{p.Label}|{p.Kind}|{p.Required}")).SequenceEqual(inputs, StringComparer.Ordinal))
            .Select(i => $"{id}: the declared inputs (key, label, kind, required) differ from the input slots of {i.Address}")];
    }

    private static List<string> FactFailures(string id, System.Text.Json.JsonElement golden, List<Instance> instances)
    {
        // A fact row's label is the recipe's label, or the key without its prefix where the recipe declares none.
        var declared = Items(golden, "inputs").Select(d => M($"in|{Text(d, "key")}|{Label(d)}|{TypeWord(Text(d, "type"))}|"))
            .Concat(Items(golden, "outputs").Select(d => M($"out|{Text(d, "key")}|{Label(d)}|{TypeWord(Text(d, "type"))}|{Text(d, "from")}")))
            .Order(StringComparer.Ordinal).ToList();
        static IEnumerable<string> Rows(Instance row) => row.Facts.Select(f => M($"{(f.Direction == FactDirection.Input ? "in" : "out")}|{f.Key}|{f.Label}|{TypeWord(f.Type.ToString())}|{(f.Direction == FactDirection.Output ? f.From : string.Empty)}"));
        return [.. instances.Where(i => i.Facts.Count > 0 && !Rows(i).Order(StringComparer.Ordinal).SequenceEqual(declared, StringComparer.Ordinal))
            .Select(i => $"{id}: the declared inputs and outputs (key, label, type, from) differ from the fact rows of {i.Address}")];
    }

    private static string Mutate(string id, Func<string, string> edit)
    {
        var path = Golden.SourcePath(typeof(WorkflowViewRecipeShapeTests).Assembly, $"Golden/Api/workflow-recipe/{id}.json");
        return edit(File.ReadAllText(path));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("workflow.incident-response", "\"cap\": 3", "\"cap\": 4")]
    [InlineData("workflow.incident-response", "\"from\": \"assess\"\n    },\n    {\n      \"key\": \"fact:history\"", "\"from\": \"comms\"\n    },\n    {\n      \"key\": \"fact:history\"")]
    [InlineData("workflow.incident-response", "\"required\": true", "\"required\": false")]
    public void The_agreement_check_fails_on_a_hand_edited_recipe_golden(string id, string from, string to)
    {
        var text = Mutate(id, t => t.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Contains(from, text, StringComparison.Ordinal);
        var golden = System.Text.Json.JsonDocument.Parse(text.Replace(from, to, StringComparison.Ordinal)).RootElement;
        Assert.NotEmpty(AgreementFailures(golden, [.. Instances().Where(i => i.Recipe.Id.Value == id)]));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_agreement_check_fails_on_an_extra_or_a_missing_declared_output()
    {
        var instances = Instances().Where(i => i.Recipe.Id.Value == "workflow.incident-response").ToList();
        var extra = System.Text.Json.JsonDocument.Parse(Mutate("workflow.incident-response", t => t.Replace("\"outputs\": [", "\"outputs\": [{\"key\":\"fact:extra\",\"label\":\"Extra\",\"type\":\"text\",\"from\":\"assess\"},", StringComparison.Ordinal))).RootElement;
        Assert.NotEmpty(AgreementFailures(extra, instances));
        var missing = System.Text.Json.JsonDocument.Parse(Mutate("workflow.incident-response", t => t.Replace("\"fact:postmortem\"", "\"fact:other\"", StringComparison.Ordinal))).RootElement;
        Assert.NotEmpty(AgreementFailures(missing, instances));
    }
}
