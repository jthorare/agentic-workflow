using Aw.Architecture.Tests.Rules;
using Aw.Architecture.Tests.TypedIds;

namespace Aw.Architecture.Tests;

/// <summary>Every §4b.1 rule over every registered production assembly (Packages.cs).</summary>
[Trait("Lane", "integration")]
public sealed class ArchitectureTests
{
    [Fact]
    public void Dependencies_point_the_right_way_inside_and_between_packages() =>
        Verify.None(DirectionRules.Check(Production.Edges, Production.Compose));

    [Fact]
    public void A_host_uses_only_the_registration_methods_of_a_compose_target() =>
        Verify.None(ComposeRules.Check(Production.Edges, Production.Compose));

    [Fact]
    public void The_ui_uses_no_sindri_type_view_models_no_avalonia_type_and_heads_no_views() => Verify.None(UiRules.Check(Production.Edges));

    [Fact]
    public void Only_the_temporal_adapter_uses_a_temporal_sdk_type() =>
        Verify.None(EngineBoundaryRules.CheckTypes(Production.Edges.Select(static e => (TypeNames.Qualified(e.Origin), e.Target.FullName))));

    [Fact]
    public void The_temporal_rule_sees_external_targets_so_it_cannot_pass_for_want_of_them() =>
        Assert.Contains(Production.Edges, e => TypeNames.NamespaceOf(e.Origin).StartsWith(EngineBoundaryRules.AllowedNamespace, StringComparison.Ordinal)
            && e.Target.FullName.StartsWith(EngineBoundaryRules.Package + ".", StringComparison.Ordinal));

    [Fact]
    public void No_slice_uses_another_slice() => Verify.None(SliceRules.Check(Production.Edges));

    [Fact]
    public void Infrastructure_types_stay_internal() =>
        Verify.None(InfrastructureRules.Check(Production.Architecture.Types, Production.Scope));

    [Fact]
    public void Every_task_returning_port_method_takes_a_cancellation_token() =>
        Verify.None(AsyncRules.Check(Production.Architecture.Types, Production.Scope));

    [Fact]
    public void Every_type_of_a_package_sits_in_a_layer_namespace() =>
        Verify.None(PlacementRules.Check(Production.Architecture.Types, Production.Scope));

    [Fact]
    public void No_namespace_cycles_in_any_assembly() => Verify.None(CycleRules.Check(Production.Architecture.Types));

    [Fact]
    public void No_dumping_ground_namespaces() => Verify.None(NamingRules.CheckNamespaces(Production.Architecture.Types));

    [Fact]
    public void No_dumping_ground_projects() =>
        Verify.None(NamingRules.CheckProjects(Solution.ProjectPaths.Select(Solution.NameOf)));

    [Fact]
    public void Service_provider_only_in_host_and_registration_lambdas() =>
        Verify.None(ServiceProviderRules.Check(Production.Edges));

    [Fact]
    public void Technology_sits_behind_ports_and_only_composition_uses_infrastructure() =>
        Verify.None(PortRules.Check(Production.Edges));

    [Fact]
    public void Every_assembly_that_declares_ids_sets_the_vogen_defaults() =>
        Verify.None(Production.Assemblies.Where(TypedIdGuard.DeclaresIds).SelectMany(TypedIdGuard.CheckDefaults)
            .Select(m => new Violation(TypedIdIds.DefaultsId, m, "copy VogenDefaults.cs from docs/csharp/typed-ids.md step 2, verbatim")));

    [Fact]
    public void Every_id_and_every_id_property_follows_the_typed_id_recipe() =>
        Verify.None(TypedIdGuard.CheckTypes(Production.Assemblies.SelectMany(a => a.GetTypes()))
            .Select(m => new Violation(TypedIdIds.TypesId, m, "follow docs/csharp/typed-ids.md (a user-code [JsonConverter], ITypedId<,>, required id properties)")));

    [Fact]
    public void Sindri_has_no_reference_to_the_deleted_recipe_namespaces()
    {
        // Issue 2258 PR 3: the recipe engine, its registry and validation, the factory line and scoping are gone.
        string[] deleted = ["Aw.Sindri.Core.Workflow.Recipes", "Aw.Sindri.Core.Workflow.Registry", "Aw.Sindri.Core.Workflow.Validation", "Aw.Sindri.Core.Line", "Aw.Sindri.Core.Scoping"];

        Verify.None(Production.Architecture.Types.Select(TypeNames.Qualified)
            .Where(name => deleted.Any(ns => name.StartsWith(ns + ".", StringComparison.Ordinal)))
            .Select(name => new Violation("ARCH-RECIPES-GONE", $"{name} still lives in a deleted recipe namespace", "delete it with the recipe engine (issue 2258 PR 3)")));
    }
}
