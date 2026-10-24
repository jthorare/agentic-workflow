using Aw.Architecture.Tests.Fixtures;
using Aw.Architecture.Tests.Rules;

namespace Aw.Architecture.Tests;

/// <summary>Each rule fails on a fixture package that breaks it, with the rule id, what it found and the fix.</summary>
[Trait("Lane", "integration")]
public sealed class FixtureTests
{
    private static readonly string[] _compliant = ["Good", "SmallGood"];
    private static readonly string[] _composing = ["ComposeHost", "ComposeTarget"];
    private static readonly ComposeEdge[] _compose =
        [new("Aw.Architecture.Tests.Fixtures.ComposeHost", "Aw.Architecture.Tests.Fixtures.ComposeTarget")];

    [Fact]
    public void The_compliant_fixtures_pass_every_rule()
    {
        var edges = FixtureSet.Edges(_compliant);
        var types = FixtureSet.Types(_compliant);
        Assert.NotEmpty(edges);
        Verify.None([
            .. DirectionRules.Check(edges), .. SliceRules.Check(edges), .. ServiceProviderRules.Check(edges),
            .. PortRules.Check(edges), .. InfrastructureRules.Check(types, FixtureSet.Roots(_compliant)),
            .. AsyncRules.Check(types, FixtureSet.Roots(_compliant)),
            .. CycleRules.Check(types), .. NamingRules.CheckNamespaces(types),
            .. PlacementRules.Check(types, FixtureSet.Roots(_compliant)),
        ]);
    }

    [Fact]
    public void Core_depending_on_its_infrastructure_is_reported() =>
        Verify.Exactly(DirectionRules.Check(FixtureSet.Edges("Direction")).Where(v => v.RuleId == DirectionRules.CoreId),
            DirectionRules.CoreId, "Direction.Core.Order -> ");

    [Fact]
    public void Features_depending_on_host_is_reported() =>
        Verify.Exactly(DirectionRules.Check(FixtureSet.Edges("Direction")).Where(v => v.RuleId == DirectionRules.FeaturesId),
            DirectionRules.FeaturesId, "Ship.ShipService -> ");

    [Fact]
    public void Infrastructure_depending_on_features_is_reported() =>
        Verify.Exactly(DirectionRules.Check(FixtureSet.Edges("Direction")).Where(v => v.RuleId == DirectionRules.InfrastructureId),
            DirectionRules.InfrastructureId, "Infrastructure.OrderStore -> ");

    [Fact]
    public void Another_package_past_its_core_is_reported() =>
        Verify.Exactly(DirectionRules.Check(FixtureSet.Edges("CrossA", "CrossB")), DirectionRules.CrossPackageId, "CrossB.Infrastructure.LedgerStore");

    [Fact]
    public void A_host_may_call_a_compose_targets_registration_methods_but_nothing_else_from_its_features_or_infrastructure() =>
        Verify.Exactly(ComposeRules.Check(FixtureSet.Edges(_composing), _compose), ComposeRules.Id,
            "ShowCommand -> Aw.Architecture.Tests.Fixtures.ComposeTarget.Features.ThingFeaturesServiceCollectionExtensions: calls Describe",
            "Composition -> Aw.Architecture.Tests.Fixtures.ComposeTarget.Features.ThingFeaturesServiceCollectionExtensions: calls Label",
            "ShowCommand -> Aw.Architecture.Tests.Fixtures.ComposeTarget.Features.Make.MakeRequest");

    [Fact]
    public void A_compose_edge_carves_the_host_out_of_the_core_only_rule_and_nothing_else() =>
        Verify.Exactly(DirectionRules.Check(FixtureSet.Edges(_composing), _compose), DirectionRules.CrossPackageId,
            "Wire.WireService -> Aw.Architecture.Tests.Fixtures.ComposeTarget.Features.ThingFeaturesServiceCollectionExtensions");

    [Fact]
    public void Without_a_compose_edge_a_host_reaching_features_or_infrastructure_is_past_the_core() =>
        Verify.Exactly(DirectionRules.Check(FixtureSet.Edges(_composing)), DirectionRules.CrossPackageId,
            "Composition -> Aw.Architecture.Tests.Fixtures.ComposeTarget.Features.ThingFeaturesServiceCollectionExtensions",
            "Composition -> Aw.Architecture.Tests.Fixtures.ComposeTarget.Infrastructure.ThingInfrastructureServiceCollectionExtensions",
            "ShowCommand -> Aw.Architecture.Tests.Fixtures.ComposeTarget.Features.ThingFeaturesServiceCollectionExtensions",
            "ShowCommand -> Aw.Architecture.Tests.Fixtures.ComposeTarget.Features.Make.MakeRequest",
            "WireService -> Aw.Architecture.Tests.Fixtures.ComposeTarget.Features.ThingFeaturesServiceCollectionExtensions");

    [Fact]
    public void Compose_edges_are_read_from_deps_yaml_by_project_prefix() =>
        Assert.Equal([new ComposeEdge("Aw.Hi", "Aw.Lo")], ComposeEdge.Parse(string.Join('\n', [
            "packages:",
            "  - id: Hi",
            "    kind: package",
            "    projectPrefix: Aw.Hi",
            "  - id: Lo",
            "    kind: package",
            "    projectPrefix: Aw.Lo",
            "  - id: Shell",
            "    kind: shell",
            "edges:",
            "  - from: Hi",
            "    to: Lo",
            "    kind: project",
            "    status: planned",
            "  - from: Hi",
            "    to: Lo",
            "    kind: compose",
            "    status: planned",
            "    note: \"kind: compose in a note is not an edge\"",
            "  - from: Shell",
            "    to: Lo",
            "    kind: cli",
            "    status: current",
            "exceptions: []",
        ])));

    [Fact]
    public void A_slice_using_another_slice_is_reported_in_both_shapes() =>
        Verify.Exactly(SliceRules.Check(FixtureSet.Edges("Slice", "SliceSmall")), SliceRules.Id, "slice Start uses slice List", "slice A uses slice B");

    [Fact]
    public void A_public_infrastructure_type_is_reported_but_the_registration_class_is_not() =>
        Verify.Exactly(InfrastructureRules.Check(FixtureSet.Types("InfraBad"), FixtureSet.Roots("InfraBad")), InfrastructureRules.Id, "OrderDto is public");

    [Fact]
    public void A_task_returning_port_method_without_a_token_is_reported_but_dispose_and_tokened_methods_are_not() =>
        Verify.Exactly(AsyncRules.Check(FixtureSet.Types("AsyncBad"), FixtureSet.Roots("AsyncBad")), AsyncRules.Id, "IFetcher.Fetch(");

    [Fact]
    public void A_namespace_cycle_is_reported() =>
        Verify.Exactly(CycleRules.Check(FixtureSet.Types("Cycle")), CycleRules.Id, "Cycle.Core.Left <-> Aw.Architecture.Tests.Fixtures.Cycle.Core.Right");

    [Fact]
    public void A_utils_namespace_is_reported() =>
        Verify.Exactly(NamingRules.CheckNamespaces(FixtureSet.Types("Naming")), NamingRules.Id, "Naming.Core.Utils");

    [Fact]
    public void A_dumping_ground_project_is_reported() =>
        Verify.Exactly(NamingRules.CheckProjects(["Aw.Judge.Core", "Aw.Shared", "Aw.Judge.Helpers.Tests"]), NamingRules.Id, "Aw.Shared", "Aw.Judge.Helpers.Tests");

    [Fact]
    public void A_service_locator_outside_host_is_reported_but_a_registration_lambda_is_not() =>
        Verify.Exactly(ServiceProviderRules.Check(FixtureSet.Edges("Locator")), ServiceProviderRules.Id,
            "Run.RunService uses IServiceProvider", "Locator.Core.Clock uses IServiceProvider", "Locator.Infrastructure.Store uses IServiceProvider");

    [Fact]
    public void A_service_locator_in_a_package_with_no_layers_is_reported() =>
        Verify.Exactly(ServiceProviderRules.Check(Edge.All(FixtureSet.Types("Unrooted"), Scope.Of([]))), ServiceProviderRules.Id,
            "Unrooted.Tools.Locator uses IServiceProvider");

    [Theory]
    [InlineData("System.IServiceProvider", true)]
    [InlineData("Microsoft.Extensions.DependencyInjection.IKeyedServiceProvider", true)]
    [InlineData("Microsoft.Extensions.DependencyInjection.IServiceScopeFactory", true)]
    [InlineData("Microsoft.Extensions.DependencyInjection.AsyncServiceScope", true)]
    [InlineData("Microsoft.Extensions.DependencyInjection.ActivatorUtilities", true)]
    [InlineData("Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions", true)]
    [InlineData("Microsoft.Extensions.DependencyInjection.IServiceCollection", false)]
    public void Service_locator_types_are_matched_by_name(string fullName, bool expected) =>
        Assert.Equal(expected, ServiceProviderRules.IsLocator(fullName));

    [Fact]
    public void Small_shape_features_depending_on_commands_is_reported() =>
        Verify.Exactly(DirectionRules.Check(FixtureSet.Edges("DirectionSmall")).Where(v => v.RuleId == DirectionRules.FeaturesId), DirectionRules.FeaturesId, "Go.GoService -> ");

    [Fact]
    public void Small_shape_infrastructure_depending_on_features_is_reported() =>
        Verify.Exactly(DirectionRules.Check(FixtureSet.Edges("DirectionSmall")).Where(v => v.RuleId == DirectionRules.InfrastructureId),
            DirectionRules.InfrastructureId, "Infrastructure.GoStore -> ");

    [Fact]
    public void Features_using_a_tier_zero_package_other_than_the_kernel_is_reported() =>
        Verify.Exactly(DirectionRules.Check(FixtureSet.Edges("TierZero")), DirectionRules.TierZeroId, "uses Aw.Kernel.Data");

    [Fact]
    public void A_namespace_under_a_package_root_that_names_no_layer_is_reported_in_both_shapes() =>
        Verify.Exactly(PlacementRules.Check(FixtureSet.Types("Placement", "PlacementSmall"), FixtureSet.Roots("Placement", "PlacementSmall")),
            PlacementRules.Id, "Placement.Domain' in", "PlacementSmall.App.Ids' in", "Fixtures.Placement' in");

    [Fact]
    public void A_namespace_outside_the_root_in_a_layer_assembly_is_reported()
    {
        var scope = new Scope(["Aw.Fx"], new Dictionary<string, string>(StringComparer.Ordinal), new Dictionary<string, LayerAssembly>(StringComparer.Ordinal) { ["Aw.Architecture.Tests"] = new("Aw.Fx", Layer.Core) });

        Verify.Exactly(PlacementRules.Check([FixtureSet.Named(typeof(Verify).FullName!)], scope), PlacementRules.Id,
            "namespace 'Aw.Architecture.Tests' in Aw.Architecture.Tests names no layer of package Aw.Fx");
    }

    [Fact]
    public void A_type_outside_every_root_in_a_host_assembly_is_host()
    {
        var scope = new Scope(["Aw.Fx"], new Dictionary<string, string>(StringComparer.Ordinal) { ["Aw.Architecture.Tests"] = "Aw.Fx" }, new Dictionary<string, LayerAssembly>(StringComparer.Ordinal));

        Assert.Equal(new Place("Aw.Fx", Layer.Host, Slice: null, IsComposition: false), Place.Of(FixtureSet.Named(typeof(Verify).FullName!), scope));
    }

    [Fact]
    public void The_production_scope_maps_a_renamed_host_and_leaves_tier_zero_out()
    {
        var scope = Production.ScopeOf([("Aw.Judge.Core", "Aw.Judge.Core"), ("Aw.Judge.Host", "judge"), ("Aw.Redaction.Core", "Aw.Redaction.Core")]);

        Assert.Equal(["Aw.Judge"], scope.Roots);
        Assert.Equal("Aw.Judge", scope.HostRootOf("judge"));
        Assert.Equal(new LayerAssembly("Aw.Judge", Layer.Core), scope.LayerOf("Aw.Judge.Core"));
        Assert.Null(scope.LayerOf("Aw.Redaction.Core"));
    }

    [Fact]
    public void A_namespace_naming_another_layer_than_its_assembly_is_reported()
    {
        const string root = "Aw.Architecture.Tests.Fixtures.Mismatch";
        var scope = new Scope([root], new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<string, LayerAssembly>(StringComparer.Ordinal) { ["Aw.Architecture.Tests"] = new(root, Layer.Core) });

        Verify.Exactly(PlacementRules.Check(FixtureSet.Types("Mismatch"), scope), PlacementRules.Id,
            "Mismatch.Infrastructure.Http' in Aw.Architecture.Tests is " + root + " Infrastructure, but Aw.Architecture.Tests holds " + root + " Core");
    }

    [Theory]
    [InlineData("<PrivateImplementationDetails>", false, "", true)]
    [InlineData("Probe", true, "", true)]
    [InlineData("EmbeddedAttribute", false, "Microsoft.CodeAnalysis.EmbeddedAttribute", true)]
    [InlineData("NullableAttribute", false, "", false)]
    [InlineData("Item", false, "System.SerializableAttribute", false)]
    public void Compiler_output_is_not_placed(string name, bool compilerGenerated, string attribute, bool expected) =>
        Assert.Equal(expected, PlacementRules.IsCompilerEmitted(name, compilerGenerated, attribute.Length == 0 ? [] : [attribute]));

    [Fact]
    public void The_global_namespace_joins_no_cycle() =>
        Verify.None(CycleRules.Check([
            .. FixtureSet.Types("Good").Where(t => TypeNames.NamespaceOf(t).EndsWith(".Host.Endpoints", StringComparison.Ordinal)),
            FixtureSet.Named("GlobalNamespaceProbe"),
        ]));

    [Fact]
    public void Verify_pairs_a_text_that_is_a_substring_of_another_with_a_distinct_violation() =>
        Verify.Exactly([new Violation("X", "Svc -> Infra", "fix"), new Violation("X", "Svc -> Core", "fix")], "X", "Svc -> ", "Svc -> Infra");

    [Theory]
    [InlineData("Aw.Judge.Host", "Aw.Judge")]
    [InlineData("Aw.Judge.App", "Aw.Judge")]
    [InlineData("judge", "Aw.Judge")]
    [InlineData("Aw.Judge.Core", null)]
    [InlineData("Aw.Kernel", null)]
    public void Scope_maps_host_assemblies_by_the_name_they_build(string assembly, string? root) =>
        Assert.Equal(root, Scope.Of(["Aw.Judge"], [KeyValuePair.Create("judge", "Aw.Judge")]).HostRootOf(assembly));

    [Fact]
    public void Technology_types_in_core_and_features_are_reported() =>
        Verify.Exactly(PortRules.Check(FixtureSet.Edges("Port")).Where(v => v.RuleId == PortRules.TechnologyId),
            PortRules.TechnologyId, "Port.Core.StoreOptions -> Aw.Kernel.Data.SqliteOptions", "Fetch.FetchService -> System.Net.Http.HttpClient");

    [Fact]
    public void Technology_types_in_small_shape_features_and_commands_are_reported() =>
        Verify.Exactly(PortRules.Check(FixtureSet.Edges("PortSmall")).Where(v => v.RuleId == PortRules.TechnologyId),
            PortRules.TechnologyId, "Go.GoService -> System.Environment: Features calls System.Environment.GetEnvironmentVariable", "Commands.ReadCommand -> System.IO.File");

    [Fact]
    public void Infrastructure_used_outside_composition_is_reported_in_both_shapes() =>
        Verify.Exactly(PortRules.Check(FixtureSet.Edges("Port", "PortSmall")).Where(v => v.RuleId == PortRules.CompositionId),
            PortRules.CompositionId, "Commands.FetchCommand -> ", "Commands.DrainCommand -> ");

    [Theory]
    [InlineData("Npgsql.NpgsqlConnection", true)]
    [InlineData("SQLitePCL.raw", true)]
    [InlineData("StackExchange.Redis.IDatabase", true)]
    [InlineData("Microsoft.Extensions.Caching.Memory.IMemoryCache", true)]
    [InlineData("System.Diagnostics.Process", true)]
    [InlineData("System.IO.File", true)]
    [InlineData("System.Net.Http.HttpClient", true)]
    [InlineData("Aw.Kernel.Data.SqliteUnitOfWork", true)]
    [InlineData("Microsoft.EntityFrameworkCore.DbContext", true)]
    [InlineData("System.Net.Dns", true)]
    [InlineData("System.Net.WebUtility", false)]
    [InlineData("System.Net.Mime.MediaTypeNames", false)]
    [InlineData("System.IO.TextWriter", false)]
    [InlineData("System.Diagnostics.Stopwatch", false)]
    [InlineData("Aw.Kernel.Result`1", false)]
    [InlineData("System.Netstandard.Thing", false)]
    public void Technology_is_matched_by_namespace_or_type(string fullName, bool expected) =>
        Assert.Equal(expected, Technology.Is(fullName));

    [Theory]
    [InlineData("System.Environment", "GetEnvironmentVariable(System.String)", true)]
    [InlineData("System.Environment", "GetFolderPath(System.Environment+SpecialFolder)", true)]
    [InlineData("System.Environment", "set_CurrentDirectory(System.String)", true)]
    [InlineData("System.Environment", "get_UserName()", true)]
    [InlineData("System.Environment", "get_MachineName()", true)]
    [InlineData("System.Environment", "get_NewLine()", false)]
    [InlineData("System.Environment", "get_ProcessorCount()", false)]
    [InlineData("System.Console", "GetEnvironmentVariable(System.String)", false)]
    public void Only_environment_reads_and_writes_are_technology(string type, string member, bool expected) =>
        Assert.Equal(expected, Technology.IsEnvironmentAccess(type, member));

    [Theory]
    [InlineData("Aw.Sindri.Core.Lease", "Aw.Sindri")]
    [InlineData("Aw.Sindri.Ui", null)]
    [InlineData("Aw.Sindri.Ui.Views", null)]
    [InlineData("Aw.Sindri.Ui.Desktop", null)]
    [InlineData("Aw.Sindri.Uix.Core", "Aw.Sindri")]
    [InlineData("Aw.Sindri.Engine.Runs", "Aw.Sindri")]
    public void An_unshaped_package_inside_a_shaped_root_belongs_to_no_package(string ns, string? root) =>
        Assert.Equal(root, (Scope.Of(["Aw.Sindri"]) with { Unshaped = ["Aw.Sindri.Ui", "Aw.Sindri.Ui.Desktop"] }).RootOf(ns));

    [Fact]
    public void The_real_ui_assemblies_are_unshaped_and_keep_the_global_rules() =>
        Assert.Equal(["Aw.Sindri.Ui", "Aw.Sindri.Ui.Desktop", "Aw.UiDriver.Avalonia", "Aw.UiDriver.Cli"], Production.Scope.Unshaped);

    [Fact]
    public void Ui_rule_reports_avalonia_in_view_models_and_views_in_heads() =>
        Verify.Exactly(UiRules.Check(FixtureSet.Edges("Ui", "UiServer"), "Aw.Architecture.Tests.Fixtures.Ui", "Aw.Architecture.Tests.Fixtures.UiServer"), UiRules.Id,
            "Ui.ViewModels.ServerCoupledViewModel uses Aw.Architecture.Tests.Fixtures.UiServer.Core.ServerStatus",
            "Ui.ViewModels.LeakyViewModel uses Avalonia.Controls.TextBlock",
            "Ui.Desktop.HeadView uses Avalonia.Controls.ContentControl",
            "Ui.Desktop.HeadView uses System.ComponentModel.INotifyPropertyChanged",
            "Ui.Desktop.HeadViewModel uses System.ComponentModel.INotifyPropertyChanged");

    [Fact]
    public void Xaml_generated_closures_are_exempt_from_the_locator_rule_and_hand_written_code_is_not() =>
        Verify.Exactly(ServiceProviderRules.Check(Edge.All(FixtureSet.Types("Xaml"), Scope.Of([]))), ServiceProviderRules.Id,
            "Xaml.HandWritten uses IServiceProvider");

    [Fact]
    public void The_real_views_xaml_locator_use_is_exempt()
    {
        var edges = Production.Edges
            .Where(e => TypeNames.Qualified(e.Origin).StartsWith("Aw.Sindri.Ui.Views.", StringComparison.Ordinal) && ServiceProviderRules.IsLocator(e.Target.FullName))
            .ToList();

        Assert.NotEmpty(edges);
        Assert.All(edges, e => Assert.True(ServiceProviderRules.IsXamlGenerated(e), $"{e.Text} is not exempt"));
    }
}
