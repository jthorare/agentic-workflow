using System.ComponentModel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Facts;
using Aw.Sindri.Ui.Views.Facts;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Facts;

/// <summary>Which viewer shows which fact type (screens §5.2, U8 T5): five shipped, everything else a payload, and U13 adds its own without touching these files.</summary>
[Lane(TestLane.Unit)]
public sealed class FactViewerRegistryTests
{
    private readonly FakeSindriApi _server = new();

    private FactViewerContext Context() => new(_server, new FakeClipboard(), new FakeTimeProvider(A.Noon), () => FactFixtures.Api, "WFI-any", _ => { });

    [Fact]
    public void Text_file_item_link_payload_have_viewers()
    {
        var registry = FactViewerRegistry.CreateDefault();
        (FactRow Row, Type Viewer)[] cases =
        [
            (FactFixtures.Row("incident-loop", "fact:severity"), typeof(TextFactViewer)),
            (FactFixtures.Row("project-build", "fact:prd"), typeof(FileFactViewer)),
            (FactFixtures.Row("done", "fact:item"), typeof(ItemFactViewer)),
            (FactFixtures.Link(), typeof(LinkFactViewer)),
            (FactFixtures.Row("project-build", "fact:project"), typeof(PayloadFactViewer)),
        ];

        Assert.All(cases, one =>
        {
            Assert.True(registry.IsRegistered(one.Row.Type), one.Row.Key);
            Assert.IsType(one.Viewer, registry.Create(one.Row, Context()));
            Assert.True(FactViewTemplates.HasView(one.Viewer), one.Viewer.Name + " has no view: its fact would read as its summary");
        });
    }

    [Fact]
    public void Unknown_type_renders_as_payload()
    {
        var registry = FactViewerRegistry.CreateDefault();
        var unknown = FactFixtures.Retyped("project-build", "fact:project", "hologram");
        var undefined = FactFixtures.Row("project-build", "fact:project") with { Type = (FactType)99 };

        Assert.Equal(FactType.Payload, unknown.Type);
        _ = Assert.IsType<PayloadFactViewer>(registry.Create(unknown, Context()));
        _ = Assert.IsType<PayloadFactViewer>(registry.Create(undefined, Context()));
    }

    [Fact]
    public void Packet_evidence_decision_render_as_payload_until_U13()
    {
        var registry = FactViewerRegistry.CreateDefault();
        FactRow[] rows =
        [
            FactFixtures.Row("project-build", "fact:packet"),
            FactFixtures.Row("project-build", "fact:evidence"),
            FactFixtures.Row("done", "fact:route"),
        ];

        Assert.Equal([FactType.Packet, FactType.Evidence, FactType.Decision], rows.Select(r => r.Type));
        Assert.All(rows, row =>
        {
            Assert.False(registry.IsRegistered(row.Type), row.Key);
            _ = Assert.IsType<PayloadFactViewer>(registry.Create(row, Context()));
        });
    }

    [Fact]
    public void Scope_map_has_its_own_viewer_and_view()
    {
        var registry = FactViewerRegistry.CreateDefault();
        var scope = FactFixtures.Row("project-build", "fact:scope");

        Assert.Equal(FactType.ScopeMap, scope.Type);
        Assert.True(registry.IsRegistered(FactType.ScopeMap));
        _ = Assert.IsType<ScopeMapViewer>(registry.Create(scope, Context()));
        Assert.True(FactViewTemplates.HasView(typeof(ScopeMapViewer)));
    }

    [Fact]
    public void Findings_have_their_own_viewer_and_view()
    {
        var registry = FactViewerRegistry.CreateDefault();
        var findings = FactFixtures.Row("project-build", "fact:project") with { Type = FactType.Findings };

        Assert.True(registry.IsRegistered(FactType.Findings));
        _ = Assert.IsType<FindingsFactViewer>(registry.Create(findings, Context()));
        Assert.True(FactViewTemplates.HasView(typeof(FindingsFactViewer)));
    }

    [Fact]
    public void The_findings_word_reads_as_the_findings_type()
    {
        var row = FactFixtures.Retyped("project-build", "fact:project", "findings");

        Assert.Equal(FactType.Findings, row.Type);
    }

    [Fact]
    public void U13_can_register_a_viewer_without_editing_U8_files()
    {
        var registry = FactViewerRegistry.CreateDefault();
        var packet = FactFixtures.Row("project-build", "fact:packet");

        registry.Register(FactType.Packet, (row, _) => new PacketViewer(row));

        Assert.True(registry.IsRegistered(FactType.Packet));
        _ = Assert.IsType<PacketViewer>(registry.Create(packet, Context()));
        _ = Assert.IsType<ScopeMapViewer>(registry.Create(FactFixtures.Row("project-build", "fact:scope"), Context()));
        _ = Assert.IsType<TextFactViewer>(registry.Create(FactFixtures.Row("incident-loop", "fact:severity"), Context()));
    }

    // What U13 writes in its own files: a viewer, registered at start-up.
    private sealed class PacketViewer(FactRow row) : IFactViewer
    {
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public FactRow Row { get; } = row;

        public Task Load(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
