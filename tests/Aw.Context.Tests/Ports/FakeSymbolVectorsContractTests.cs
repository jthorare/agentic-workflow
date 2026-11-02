using Aw.Context.Testing.Glossary;

namespace Aw.Context.Tests.Ports;

[Trait("Lane", "unit")]
public sealed class FakeSymbolVectorsContractTests : SymbolVectorsContract<FakeSymbolVectors>
{
    protected override FakeSymbolVectors Create() => new FakeSymbolVectors().With("src/Shift.cs", "ShiftSwap", 1f, 0f);
}
