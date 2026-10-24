using Aw.CodeIndex.Core.Graph;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Tests.Graph;

// The pure span rule (spec §5.6): a graph symbol's text runs from its line to the line before the next graph symbol of the file, or to the end of the file.
[Trait("Lane", "unit")]
public sealed class GraphBodiesTests
{
    private static SymbolRecord Symbol(string name, int line, SymbolKind kind = SymbolKind.GraphSymbol)
        => new(SymbolRef.From($"web:src/a.ts#{name}"), name, name, kind, Arity: 0, line, line, Exported: false, Utility: false,
            Signature: string.Empty, ShapeHash: string.Empty, TokenCount: 0, Complexity: 0, ReadOnlyMemory<byte>.Empty, [], Body: null, []);

    private static List<(string Name, string? Body)> Attach(string text, params SymbolRecord[] symbols)
        => [.. GraphBodies.Attach(new FileRecord("src/a.ts", "blob", text.Length, "typescript", Parsed: false, symbols), text, s => s).Symbols.Select(s => (s.Name, s.Body))];

    [Fact]
    public void Each_body_stops_on_the_line_before_the_next_symbol_and_the_last_runs_to_the_end_of_the_file()
    {
        var bodies = Attach("function a() {\n  1;\n}\nfunction b() {\n  2;\n}\nfunction c() {\n  3;\n}", Symbol("a", 1), Symbol("b", 4), Symbol("c", 7));

        Assert.Equal(
            [("a", "function a() {\n  1;\n}"), ("b", "function b() {\n  2;\n}"), ("c", "function c() {\n  3;\n}")],
            bodies);
    }

    [Fact]
    public void Two_symbols_on_the_same_line_share_one_body()
    {
        var bodies = Attach("const a = () => 1, b = () => 2;\nfunction c() {}", Symbol("a", 1), Symbol("b", 1), Symbol("c", 2));

        Assert.Equal([("a", "const a = () => 1, b = () => 2;"), ("b", "const a = () => 1, b = () => 2;"), ("c", "function c() {}")], bodies);
    }

    [Fact]
    public void A_symbol_past_the_end_of_the_file_has_an_empty_body()
        => Assert.Equal([("a", "x"), ("gone", string.Empty)], Attach("x", Symbol("a", 1), Symbol("gone", 9)));

    [Fact]
    public void A_class_symbol_gets_an_empty_body_so_it_is_embedded_by_name_and_signature()
        => Assert.Equal([("Cart", string.Empty), ("f", "function f() {}")], Attach("class Cart {}\nfunction f() {}", Symbol("Cart", 1, SymbolKind.Class), Symbol("f", 2)));

    [Fact]
    public void Carriage_returns_and_a_leading_byte_order_mark_are_dropped()
        => Assert.Equal([("a", "function a() {\n}"), ("b", "function b() {}")], Attach("﻿function a() {\r\n}\r\nfunction b() {}\r\n", Symbol("a", 1), Symbol("b", 3)).Select(x => (x.Name, x.Body?.TrimEnd('\n'))));
}
