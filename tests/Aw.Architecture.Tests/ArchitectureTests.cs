using Aw.Architecture.Tests.Rules;
using Aw.Architecture.Tests.TypedIds;

namespace Aw.Architecture.Tests;

/// <summary>Every §4b.1 rule over every registered production assembly (Packages.cs).</summary>
[Trait("Lane", "unit")]
public sealed class ArchitectureTests
{
    [Fact]
    public void Dependencies_point_the_right_way_inside_and_between_packages() =>
        Verify.None(DirectionRules.Check(Production.Edges, Production.Compose));

    [Fact]
    public void A_host_uses_only_the_registration_methods_of_a_compose_target() =>
        Verify.None(ComposeRules.Check(Production.Edges, Production.Compose));

    [Fact]
    public void No_slice_uses_another_slice() => Verify.None(SliceRules.Check(Production.Edges));

    [Fact]
    public void Infrastructure_types_stay_internal() =>
        Verify.None(InfrastructureRules.Check(Production.Architecture.Types, Production.Scope));

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
}
