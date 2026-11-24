using Aw.Architecture.Tests.Rules;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Architecture.Tests;

/// <summary>A test run binds every port to its test version unless the port is read-only (spec 2026-11-12 §5.1, P4a).</summary>
[Trait("Lane", "integration")]
public sealed class GraphBlockFenceTests
{
    [Fact]
    public void Every_graph_block_constructor_parameter_has_a_test_binding_or_is_read_only() =>
        Verify.None(GraphBlockFenceRules.Check(GraphBlocks(), GraphTestBindings.Bound, GraphTestBindings.ReadOnly));

    [Fact]
    public void A_port_with_neither_a_test_binding_nor_a_read_only_mark_is_reported()
    {
        using var port = new MemoryStream();
        Assert.NotNull(new NeedsAPort(port, TimeProvider.System));

        Verify.Exactly(
            GraphBlockFenceRules.Check([typeof(NeedsAPort)], new HashSet<Type>(), new HashSet<Type> { typeof(TimeProvider) }),
            GraphBlockFenceRules.Id,
            nameof(IDisposable));
    }

    private static IEnumerable<Type> GraphBlocks() =>
        Production.Assemblies
            .SelectMany(static a => a.GetTypes())
            .Where(static t => t is { IsClass: true, IsAbstract: false } && typeof(IGraphBlock).IsAssignableFrom(t));

    private sealed class NeedsAPort(IDisposable port, TimeProvider clock)
    {
        public IDisposable Port { get; } = port;

        public TimeProvider Clock { get; } = clock;
    }
}
