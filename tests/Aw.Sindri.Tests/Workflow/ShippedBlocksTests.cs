using System.Reflection;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Features.Blocks.Gate;
using Aw.Sindri.Host;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The blocks and events this build ships (spec §12), read from the real composition.</summary>
public sealed class ShippedBlocksTests
{
    private static readonly string _stateRoot = Path.Combine(Path.GetTempPath(), "aw-sindri-blocks-test");

    [Fact, Trait("Lane", "unit")]
    public async Task The_catalog_holds_exactly_the_section_12_blocks_and_events()
    {
        await using var provider = Composed();
        var catalog = provider.GetRequiredService<BlockCatalog>();

        Assert.Equal(
            [
                "effect.claim.ref", "effect.line.report", "effect.notify.desk", "effect.pr.create-stacked", "effect.pr.merge",
                "effect.pr.merge-docs", "effect.pr.push", "effect.pr.record", "effect.pr.reply", "effect.pr.rerun-checks", "effect.pr.restack",
                "evidence.check", "gate.budget.available", "gate.pr.autofix-cap", "gate.pr.base-is-default", "gate.pr.diff-scope",
                "gate.pr.secret-scan", "gate.revision-bound", "gate.route.startable", "input.context.code", "input.context.history",
                "input.context.lenses", "input.handoff-note", "input.phase-packet", "input.pr.context", "step.babysit.watch",
                "step.brief.questions", "step.brief.seed", "step.brief.write", "step.bugfix.judge", "step.bugfix.recheck",
                "step.bugfix.record-candidate", "step.bugfix.start-attempt", "step.build.amend", "step.build.checks", "step.build.green",
                "step.build.prepare", "step.build.red", "step.build.red-check", "step.build.scope-desk", "step.checks", "step.design.apply",
                "step.design.check", "step.design.draft", "step.design.evidence", "step.design.intake", "step.design.lens-desk",
                "step.design.revise", "step.desk.choose", "step.docs.check", "step.docs.scope", "step.docs.update", "step.evidence.api-plan",
                "step.evidence.api-run", "step.evidence.combine", "step.evidence.db-plan", "step.evidence.db-run", "step.evidence.desk",
                "step.evidence.none", "step.evidence.ui-plan", "step.evidence.ui-run", "step.handtest.run", "step.intake.ticket",
                "step.interview.answer", "step.interview.apply", "step.interview.questions", "step.interview.record", "step.judge.choose",
                "step.lens.ask", "step.lens.log", "step.lens.round", "step.lens.run", "step.owner.decide", "step.phase.decide",
                "step.phase.drift", "step.phase.seed", "step.plan.check", "step.plan.evidence", "step.plan.exec-review", "step.plan.freeze",
                "step.plan.graph", "step.plan.holdout", "step.plan.intake", "step.plan.questions", "step.plan.review", "step.plan.settle",
                "step.pr.ci-fix", "step.pr.docs-decide", "step.pr.docs-merge-decide", "step.pr.fix-comment", "step.pr.held-comment",
                "step.pr.intake", "step.pr.next-comment", "step.pr.owner-comment", "step.pr.ready-check", "step.pr.reply-draft",
                "step.pr.rework-notes", "step.pr.snapshot", "step.produce", "step.prove.before-after", "step.prove.evidence",
                "step.prove.fail-log", "step.prove.holdout", "step.prove.holdout-log", "step.prove.intake", "step.prove.rerun-scripts",
                "step.recheck.log", "step.recheck.plan", "step.report.write", "step.reproduce.test-check", "step.reproduce.ui-check",
                "step.review.apply-answer", "step.review.fix", "step.review.log", "step.review.recheck", "step.review.record",
                "step.review.round", "step.rootcause.investigate", "step.scope.anchor", "step.scope.challenge", "step.scope.challenge-log",
                "step.scope.check", "step.scope.draft", "step.scope.evidence", "step.scope.freeze", "step.scope.gather", "step.scope.intake",
                "step.scope.propose", "step.scope.read-brief", "step.scope.read-change", "step.scope.read-tracker", "step.scope.reanchor",
                "step.scope.review", "step.scope.withdraw", "step.ship.freeze", "step.ship.green", "step.ship.open", "step.ship.pack",
                "step.ship.return", "step.verify", "step.verify.design", "step.verify.journey",
            ],
            catalog.Blocks.Select(b => b.Id.Value).Order(StringComparer.Ordinal),
            StringComparer.Ordinal);
        Assert.Equal(["claim.created", "item.routed", "notify.delivered", "pr.changed", "pr.docsMerged", "pr.mergeResult", "pr.pushed", "pr.restacked", "pr.stacked"], catalog.Events.Select(e => e.Kind.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    /// <summary>Engine P8 Q4: the produce Step runs on an agent session when the build has the runtime, and stays deskCapable so a box with no agent target runs it at the desk (D7).</summary>
    [Fact, Trait("Lane", "unit")]
    public void The_produce_step_runs_on_an_agent_and_stays_desk_capable()
    {
        var produce = FixtureCatalog.Shipped().Find(BlockId.From("step.produce"));

        Assert.Equal((BlockRuntime.Agent, true), (produce?.Runtime, produce?.DeskCapable));
    }

    /// <summary>The Steps that commit (tests, code) or build and run the app in the run's worktree need the policy that lets them write it; WriteScope grants that worktree and nothing else.</summary>
    [Theory, Trait("Lane", "unit")]
    [InlineData("step.build.red")]
    [InlineData("step.build.green")]
    [InlineData("step.prove.holdout")]
    public void The_agent_steps_that_write_their_worktree_carry_write_scope(string id)
    {
        var step = FixtureCatalog.Shipped().Find(BlockId.From(id));
        var address = InstanceAddress.From("T-1/step/1");
        var run = new AgentStepRun(WorkflowInstanceId.Of(address), address, RunToken.From("1.1"), new StepRef("s", id), step!.Capabilities.HasFlag(Capability.WriteScope));
        var target = new AgentStepTarget(ItemId.From("ITM-" + new string('a', 32)), AgentId.From("claude-code"), "/work/app");

        var request = AgentLaunches.RequestFor(run, target);

        Assert.Equal((BlockRuntime.Agent, StepPermissionMode.Default, request.Worktree), (step.Runtime, request.Step?.Policy?.Mode, request.Step?.Policy?.WritablePath));
    }

    [Fact, Trait("Lane", "unit")]
    public void No_shipped_descriptor_says_human_only_false()
    {
        var catalog = FixtureCatalog.Shipped();

        Assert.DoesNotContain(catalog.Blocks, static b => b.HumanOnly == false);   // written only when true (DT spec 8.5): false would change the descriptor hash for nothing
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_two_gates_and_the_two_effects_have_runtimes_and_the_desk_steps_run_on_the_desk()
    {
        await using var provider = Composed();
        await using var scope = provider.CreateAsyncScope();

        Assert.Equal([typeof(BudgetAvailableGate), typeof(RouteStartableGate)], scope.ServiceProvider.GetServices<IGate>().Select(static g => g.GetType()));   // engine P7 adds the route stub
        Assert.Empty(scope.ServiceProvider.GetServices<IStep>());
        Assert.Equal([typeof(ClaimRefEffect), typeof(NotifyDeskEffect), typeof(PrMergeEffect), typeof(PrMergeDocsEffect), typeof(PrReplyEffect)], scope.ServiceProvider.GetServices<IEffectKind>().Select(static e => e.GetType()));   // engine P6
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("step.produce", new[] { "done" }, "fact:output", new[] { "done" })]
    [InlineData("step.checks", new[] { "passed", "failed" }, "fact:findings", new[] { "failed" })]
    [InlineData("step.verify", new[] { "accepted", "rejected" }, "fact:findings", new[] { "rejected" })]
    public void Each_desk_step_declares_its_outcomes_and_required_output(string id, string[] outcomes, string output, string[] requiredOn)
    {
        var step = FixtureCatalog.Shipped().Find(BlockId.From(id))!;   // why: the catalog test pins every shipped id

        Assert.True(step.DeskCapable);
        Assert.Equal(outcomes, step.Outcomes.Select(o => o.Value), StringComparer.Ordinal);
        var spec = Assert.Single(step.Outputs);
        Assert.Equal(output, spec.Fact.Value);
        Assert.Equal(requiredOn, spec.RequiredOn.Select(o => o.Value), StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("effect.claim.ref", Capability.Effects | Capability.WriteScope)]
    [InlineData("effect.notify.desk", Capability.Effects)]
    [InlineData("effect.pr.merge-docs", Capability.Effects)]
    [InlineData("gate.budget.available", Capability.None)]
    [InlineData("gate.route.startable", Capability.None)]
    public void Each_block_declares_what_it_reaches(string id, Capability capabilities) =>
        Assert.Equal(capabilities, FixtureCatalog.Shipped().Find(BlockId.From(id))!.Capabilities);   // why: the catalog test pins every shipped id

    [Theory, Trait("Lane", "unit")]
    [InlineData("item.routed", "service:router-stub", EventRoute.Item, new[] { "startable", "notStartable" }, new[] { "fact:stepSpec" })]
    [InlineData("claim.created", "system:dispatcher", EventRoute.Causation, new[] { "created", "claimExists" }, new string[0])]
    [InlineData("notify.delivered", "system:dispatcher", EventRoute.Causation, new[] { "delivered" }, new string[0])]
    public void Each_event_declares_its_producer_route_outcomes_and_facts(string kind, string producer, EventRoute route, string[] outcomes, string[] facts)
    {
        var e = FixtureCatalog.Shipped().FindEvent(EventKind.From(kind))!;   // why: the catalog test pins every shipped kind

        Assert.Equal(producer, e.Producer);
        Assert.Equal(route, e.Route);
        Assert.Equal(outcomes, e.Outcomes.Select(o => o.Value), StringComparer.Ordinal);
        Assert.Equal(facts, e.Facts.Select(f => f.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_choice_steps_are_desk_capable_descriptors_without_runtimes()
    {
        await using var provider = Composed();
        await using var scope = provider.CreateAsyncScope();
        var catalog = provider.GetRequiredService<BlockCatalog>();

        foreach (var id in new[] { "step.judge.choose", "step.desk.choose" })
        {
            var step = catalog.Find(BlockId.From(id))!;   // why: the catalog test pins both ids
            Assert.True(step.DeskCapable);
            Assert.Equal(BlockKind.Step, step.Kind);
            var decision = Assert.Single(step.Outputs);
            Assert.Equal(("fact:decision", true, true), (decision.Fact.Value, decision.Every, decision.EngineFilled));
            Assert.Empty(decision.RequiredOn);
        }

        Assert.True(catalog.Find(BlockId.From("step.desk.choose"))!.HumanOnly);   // why: the catalog test pins both ids
        Assert.Equal(BlockRuntime.Block, catalog.Find(BlockId.From("step.judge.choose"))!.Runtime);

        Assert.Empty(scope.ServiceProvider.GetServices<IStep>());   // no runtime until the block runtime (DT-J)
        // the scoping station's and the design lens loop's Steps listed here are the block runtimes of this build (BS-1, BS-4); the judge's comes with J3 and moves its contract to BlockContract
        Assert.Equal(
            ["step.brief.seed", "step.bugfix.judge", "step.bugfix.recheck", "step.bugfix.record-candidate", "step.bugfix.start-attempt", "step.build.amend", "step.build.checks", "step.build.prepare", "step.build.red-check", "step.design.check", "step.design.evidence", "step.design.intake", "step.docs.check", "step.evidence.combine", "step.evidence.none", "step.interview.record", "step.lens.log", "step.lens.round", "step.phase.drift", "step.phase.seed", "step.plan.check", "step.plan.evidence", "step.plan.freeze", "step.plan.intake", "step.plan.questions", "step.plan.settle", "step.prove.evidence", "step.prove.fail-log", "step.prove.holdout-log", "step.prove.intake", "step.review.log", "step.review.record", "step.review.round", "step.scope.anchor", "step.scope.challenge-log", "step.scope.check", "step.scope.evidence", "step.scope.freeze", "step.scope.gather", "step.scope.intake", "step.scope.propose", "step.scope.read-brief", "step.scope.read-change", "step.scope.read-tracker", "step.scope.reanchor", "step.scope.withdraw", "step.ship.freeze", "step.ship.green", "step.ship.return"],
            provider.GetServices<BlockStepRegistration>().Select(static r => r.Step.Value).Order(StringComparer.Ordinal),
            StringComparer.Ordinal);
        Assert.Equal(49, scope.ServiceProvider.GetServices<IBlockStep>().Count());
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_block_runtime_registration_names_a_Step_whose_descriptor_says_block()
    {
        using var provider = Composed();
        var catalog = provider.GetRequiredService<BlockCatalog>();

        var registered = provider.GetServices<BlockStepRegistration>().Select(static r => r.Step).ToList();

        Assert.NotEmpty(registered);
        Assert.All(registered, id => Assert.Equal(BlockRuntime.Block, catalog.Find(id)?.Runtime));   // a Step registered by id (AddBlockRuntime) has its descriptor elsewhere, so this is where a mismatch shows
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_outcomes_from_and_inputs_from_names_a_list_parameter()
    {
        var from = FixtureCatalog.WithFixtures().Blocks.Where(b => b.OutcomesFrom is not null || b.InputsFrom is not null).ToList();

        Assert.Equal(["step.desk.choose", "step.judge.choose"], from.Select(b => b.Id.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var block in from)
        {
            foreach (var named in new[] { block.OutcomesFrom, block.InputsFrom }.OfType<string>())
            {
                // "choices", or "<stateParam>.<listParam>" for a twin: the first names a text parameter holding a state, the second a list parameter of that state's Step
                var parameter = named.Contains('.', StringComparison.Ordinal) ? named[..named.IndexOf('.', StringComparison.Ordinal)] : named;
                var type = Assert.Single(block.Parameters, p => p.Name == parameter).Type;
                Assert.Equal(named.Contains('.', StringComparison.Ordinal) ? ParameterType.Text : ParameterType.List, type);
            }
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void A_descriptor_without_the_new_fields_serializes_without_them()
    {
        var json = System.Text.Encoding.UTF8.GetString(AwJson.Serialize(FixtureCatalog.Shipped().Find(BlockId.From("step.checks")), SliceHarness.Json));

        Assert.DoesNotContain("outcomesFrom", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("inputsFrom", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("every", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("engineFilled", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("humanOnly", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("runtime", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_descriptor_kind_matches_its_id_prefix()
    {
        var blocks = FixtureCatalog.WithFixtures().Blocks;

        Assert.NotEmpty(blocks);
        Assert.All(blocks, b => Assert.Equal(BlockKinds.Of(b.Id), b.Kind));
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_gate_has_a_rung_and_no_other_block_does()
    {
        var blocks = FixtureCatalog.WithFixtures().Blocks;

        Assert.All(blocks.Where(b => b.Kind == BlockKind.Gate), b => Assert.NotNull(b.Rung));
        Assert.All(blocks.Where(b => b.Kind != BlockKind.Gate), b => Assert.Null(b.Rung));
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_effect_is_capability_bearing() =>
        Assert.All(FixtureCatalog.WithFixtures().Blocks.Where(b => b.Kind == BlockKind.Effect), b => Assert.True(b.Capabilities.HasFlag(Capability.Effects)));

    [Fact, Trait("Lane", "unit")]
    public async Task Every_composed_block_has_exactly_one_descriptor_contract()
    {
        await using var provider = Composed();
        var catalogIds = provider.GetRequiredService<BlockCatalog>().Blocks.Select(b => b.Id.Value).ToHashSet(StringComparer.Ordinal);
        var contractIds = typeof(ShippedBlocksTests).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(DescriptorContract).IsAssignableFrom(t))
            .Select(t => ((BlockDescriptor)typeof(DescriptorContract)
                .GetProperty("Descriptor", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(Activator.CreateInstance(t, nonPublic: true))!).Id.Value)
            .Where(static id => !string.Equals(id, "input.prove.echo", StringComparison.Ordinal) && !string.Equals(id, "step.prove.echo", StringComparison.Ordinal))   // test-only: composed (and catalogued) only under the prove gate
            .ToList();

        var withoutContract = catalogIds.Except(contractIds, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var withoutBlock = contractIds.Except(catalogIds, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var duplicated = contractIds.GroupBy(i => i, StringComparer.Ordinal).Where(g => g.Skip(1).Any()).Select(g => g.Key).Order(StringComparer.Ordinal).ToList();

        Assert.True(
            withoutContract.Count == 0 && withoutBlock.Count == 0 && duplicated.Count == 0,
            $"catalog blocks with no contract: [{string.Join(", ", withoutContract)}]; contracts for no catalog block: [{string.Join(", ", withoutBlock)}]; duplicated contracts: [{string.Join(", ", duplicated)}]");
    }

    private static ServiceProvider Composed() =>
        Composition.Compose(new ServiceCollection(), name => name == "AW_STATE_DIR" ? _stateRoot : null, new FakeTimeProvider())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
}
