using Aw.Kernel.Testing;

namespace Aw.Sindri.Tests.LinearWrites;

/// <summary>The real write gate over Context (Linear hub spec §5.1): the allow path and each refusal (#2261).</summary>
[Lane(TestLane.Unit)]
public sealed class ContextLinearWriteGateTests
{
    [Fact]
    public Task The_real_gate_has_allow_and_refuse_tests() => throw new NotSupportedException("ContextLinearWriteGate has no unit tests yet (#2261)");
}
