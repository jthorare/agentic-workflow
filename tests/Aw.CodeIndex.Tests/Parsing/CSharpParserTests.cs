using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Infrastructure.Parsing;

namespace Aw.CodeIndex.Tests.Parsing;

public sealed class CSharpParserTests
{
    private static readonly CSharpParser _parser = new();

    private static ParseOutcome Parse(string text, CSharpParser? parser = null) =>
        (parser ?? _parser).Parse("src/A.cs", text, TestContext.Current.CancellationToken).Value;

    private static IReadOnlyList<ParsedSymbol> Symbols(string text) => Parse(text).Symbols;

    private static ParsedSymbol Symbol(string text, string qualified) => Symbols(text).Single(s => s.Qualified == qualified);

    private static IReadOnlyList<(string Name, int Arity, string Receiver, int Line, EdgeKind Kind)> Sites(string text, string qualified) =>
        [.. Symbol(text, qualified).CallSites.Select(c => (c.Name, c.Arity, c.Receiver, c.Line, c.Kind))];

    [Theory, Trait("Lane", "unit")]
    [InlineData("src/A.cs", true)]
    [InlineData("A.CS", true)]
    [InlineData("a.csx", false)]
    [InlineData("a.cs.txt", false)]
    [InlineData("a.ts", false)]
    public void It_handles_cs_files_only(string path, bool expected) => Assert.Equal(expected, _parser.Handles(path));

    [Fact, Trait("Lane", "unit")]
    public void Symbols_carry_namespace_nesting_kind_arity_and_a_one_based_span()
    {
        const string text = """
            namespace Outer.Inner
            {
                public class Box<T>
                {
                    public int Count() => 0;

                    public sealed record Pair(int A, int B);

                    public enum Mode { One, Two }
                }

                public interface IGo { void Run(int a, string b); }
            }
            """;

        var symbols = Symbols(text).ToDictionary(s => s.Qualified, StringComparer.Ordinal);

        Assert.Equal((SymbolKind.Class, 1, 3, 10), (symbols["Outer.Inner.Box"].Kind, symbols["Outer.Inner.Box"].Arity, symbols["Outer.Inner.Box"].StartLine, symbols["Outer.Inner.Box"].EndLine));
        Assert.Equal((SymbolKind.Method, 0, 5), (symbols["Outer.Inner.Box.Count"].Kind, symbols["Outer.Inner.Box.Count"].Arity, symbols["Outer.Inner.Box.Count"].StartLine));
        Assert.Equal(SymbolKind.Record, symbols["Outer.Inner.Box.Pair"].Kind);
        Assert.Equal(SymbolKind.Enum, symbols["Outer.Inner.Box.Mode"].Kind);
        Assert.Equal((SymbolKind.Method, 2, "int,string"), (symbols["Outer.Inner.IGo.Run"].Kind, symbols["Outer.Inner.IGo.Run"].Arity, symbols["Outer.Inner.IGo.Run"].ParameterTypesText));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_start_line_and_the_signature_skip_attributes_and_the_signature_is_one_normalized_line()
    {
        const string text = """
            class A
            {
                [Obsolete]
                [Other]
                public static   int
                    Add<T>(this int a,
                           T b) where T : class
                {
                    return a;
                }
            }
            """;

        var add = Symbol(text, "A.Add");

        Assert.Equal(5, add.StartLine);
        Assert.Equal(10, add.EndLine);
        Assert.Equal("public static int Add<T>(this int a, T b) where T : class", add.Signature);
        Assert.Equal(1, add.Arity);   // the 'this' parameter is not counted
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("public class A { public int M() => 0; }", "A.M", true)]
    [InlineData("public class A { protected int M() => 0; }", "A.M", true)]
    [InlineData("public class A { protected internal int M() => 0; }", "A.M", true)]
    [InlineData("public class A { private protected int M() => 0; }", "A.M", false)]
    [InlineData("public class A { internal int M() => 0; }", "A.M", false)]
    [InlineData("public class A { int M() => 0; }", "A.M", false)]
    [InlineData("class A { public int M() => 0; }", "A.M", false)]
    [InlineData("public class A { public class B { public int M() => 0; } }", "A.B.M", true)]
    [InlineData("public class A { private class B { public int M() => 0; } }", "A.B.M", false)]
    [InlineData("public interface I { int M(); }", "I.M", true)]
    [InlineData("public class A { public int P { get; private set; } public int Q { get { return 1; } private set { } } }", "A.Q.set", false)]
    [InlineData("public class A { public int Q { get { return 1; } private set { } } }", "A.Q.get", true)]
    public void Exported_means_public_or_protected_on_a_visible_type(string text, string qualified, bool expected) =>
        Assert.Equal(expected, Symbol(text, qualified).Exported);

    [Fact, Trait("Lane", "unit")]
    public void Locals_lambdas_and_accessors_get_their_own_qualified_names_and_kinds()
    {
        const string text = """
            class A
            {
                public int Prop { get { return F(1); } set { } }
                public int Expr => F(2);
                public int F(int x)
                {
                    int Local(int y) => y;
                    Func<int, int> twice = z => Local(z);
                    var plain = 3;
                    return twice(x);
                }
            }
            """;

        var symbols = Symbols(text).ToDictionary(s => s.Qualified, s => s.Kind, StringComparer.Ordinal);

        Assert.Equal(SymbolKind.Accessor, symbols["A.Prop.get"]);
        Assert.Equal(SymbolKind.Accessor, symbols["A.Prop.set"]);
        Assert.Equal(SymbolKind.Accessor, symbols["A.Expr.get"]);
        Assert.Equal(SymbolKind.LocalFunction, symbols["A.F.Local"]);
        Assert.Equal(SymbolKind.Lambda, symbols["A.F.twice"]);
        Assert.DoesNotContain("A.F.plain", symbols.Keys, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Auto_properties_fields_and_enum_members_are_not_symbols()
    {
        var names = Symbols("class A { public int P { get; set; } int _f; enum E { X } int Q { get; } }").Select(s => s.Qualified);

        Assert.Equal(["A", "A.E"], names, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Operators_destructors_and_indexers_are_symbols()
    {
        const string text = "class A { public static A operator +(A a, A b) => a; ~A() { } public int this[int i] { get { return i; } } }";

        var names = Symbols(text).Select(s => s.Qualified);

        Assert.Equal(["A", "A.operator +", "A.~A", "A.this[].get"], names, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Receivers_follow_the_stated_rules()
    {
        const string text = """
            class A
            {
                public string Name { get; }
                void Go(B b)
                {
                    Local();
                    this.Self();
                    base.Up();
                    Math.Abs(1);
                    string.Join(",", "a");
                    b.Other();
                    Name.Trim();
                    Factory.Make<int>().Chain();
                    b?.Maybe();
                    var x = new Widget(1, 2);
                    var y = new Ns.Deep.Gadget();
                    var z = new List<int> { 1 };
                    var w = nameof(b);
                    new();
                }
            }
            """;

        var sites = Sites(text, "A.Go").Select(s => (s.Name, s.Arity, s.Receiver)).ToList();

        Assert.Equal(
            [
                ("Local", 0, "this"), ("Self", 0, "this"), ("Up", 0, "base"), ("Abs", 1, "type:Math"), ("Join", 2, "type:string"),
                ("Other", 0, "other"), ("Trim", 0, "other"), ("Chain", 0, "other"), ("Make", 0, "type:Factory"), ("Maybe", 0, "other"),
                ("Widget", 2, "type:Widget"), ("Gadget", 0, "type:Gadget"), ("List", 0, "type:List"),
            ],
            sites);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_call_is_on_the_line_of_its_name_and_a_chain_gets_one_site_per_call()
    {
        const string text = """
            class A
            {
                void Go()
                {
                    Source()
                        .Where(1)
                        .Select(2);
                }
            }
            """;

        var sites = Sites(text, "A.Go").Select(s => (s.Name, s.Line)).Order().ToList();

        Assert.Equal([("Select", 7), ("Source", 5), ("Where", 6)], sites);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_local_function_or_method_used_as_a_value_is_a_method_group_with_arity_minus_one()
    {
        const string text = """
            class A
            {
                int Handler(int x) => x;
                void Go(int[] xs, int value)
                {
                    int Local(int y) => y;
                    xs.Select(Local);
                    xs.Select(Handler);
                    Func<int, int> f = Local;
                    Use(value);
                }
            }
            """;

        var groups = Sites(text, "A.Go").Where(s => s.Arity == -1).Select(s => (s.Name, s.Receiver, s.Line)).ToList();

        Assert.Equal([("Local", "this", 7), ("Handler", "this", 8), ("Local", "this", 9)], groups);
    }

    [Fact, Trait("Lane", "unit")]
    public void Inheritance_gives_one_site_per_base_type_with_its_type_argument_count()
    {
        const string text = """
            interface IA { }
            class B<T> { }
            class C : B<int>, IA, Ns.Other<string, int>
            {
            }
            """;

        var sites = Sites(text, "C").Select(s => (s.Kind, s.Name, s.Arity, s.Receiver, s.Line)).ToList();

        Assert.Equal(
            [
                (EdgeKind.Inherits, "B", 1, "type:B", 3), (EdgeKind.Inherits, "IA", 0, "type:IA", 3), (EdgeKind.Inherits, "Other", 2, "type:Other", 3),
            ],
            sites);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_call_in_a_field_initializer_belongs_to_the_type_and_a_call_in_global_code_to_nobody()
    {
        var symbols = Symbols("class A { int f = Compute(); } Outside();");

        Assert.Equal("Compute", Assert.Single(symbols.Single(s => s.Qualified == "A").CallSites).Name);
        Assert.DoesNotContain(symbols.Where(s => s.Qualified != "A"), s => s.CallSites.Count > 0);
    }

    [Fact, Trait("Lane", "unit")]
    public void Complexity_counts_decisions_and_short_circuits_but_not_nested_local_functions()
    {
        const string text = """
            class A
            {
                int F(int a, bool b)
                {
                    int Inner(int x) { if (x > 0) { return 1; } return 0; }
                    if (a > 0 && b || a < -1) { a++; }
                    foreach (var i in new[] { 1 }) { while (a > 0) { a--; } }
                    return a switch { 1 => 1, _ => b ? 2 : 3 } + (a ?? 0);
                }
            }
            """;

        Assert.Equal(10, Symbol(text, "A.F").Complexity);   // 1 + if + && + || + foreach + while + two switch arms + ?: + ??
        Assert.Equal(2, Symbol(text, "A.F.Inner").Complexity);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_shape_hash_ignores_identifiers_and_literals_and_sees_structure()
    {
        const string one = "class A { int F(int a) { var b = a + 1; return b * 2; } }";
        const string renamed = "class Z { int Other(int q) { var w = q + 99; return w * 3; } }";
        const string different = "class A { int F(int a) { var b = a - 1; return b * 2; } }";

        Assert.Equal(Symbol(one, "A.F").ShapeHash, Symbol(renamed, "Z.Other").ShapeHash);
        Assert.NotEqual(Symbol(one, "A.F").ShapeHash, Symbol(different, "A.F").ShapeHash, StringComparer.Ordinal);
        Assert.Equal(16, Symbol(one, "A.F").ShapeHash.Length);
    }

    [Fact, Trait("Lane", "unit")]
    public void Tokens_blank_identifiers_and_literals_for_callables_and_are_empty_for_types()
    {
        var symbols = Symbols("class A { int F(int a) { return a + 1; } }");

        Assert.Equal(["int", "id", "(", "int", "id", ")", "{", "return", "id", "+", "lit", ";", "}"], symbols.Single(s => s.Qualified == "A.F").Tokens);
        Assert.Empty(symbols.Single(s => s.Qualified == "A").Tokens);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_callable_keeps_its_text_as_the_body_and_a_type_has_none()
    {
        var symbols = Symbols("class A { int F() { return 1; } }");

        Assert.Equal("int F() { return 1; }", symbols.Single(s => s.Qualified == "A.F").Body);
        Assert.Null(symbols.Single(s => s.Qualified == "A").Body);
    }

    [Fact, Trait("Lane", "unit")]
    public void Overloads_differ_in_their_parameter_type_text()
    {
        var texts = Symbols("class A { void F(int a) { } void F(string a) { } void F(ref int a) { } }").Where(s => s.Name == "F").Select(s => s.ParameterTypesText);

        Assert.Equal(["int", "string", "ref int"], texts, StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("class")]
    [InlineData("class A {")]
    [InlineData("class A { void F( { } ")]
    [InlineData("\"unterminated")]
    [InlineData("/* never closed")]
    [InlineData("@\"verbatim never closed")]
    [InlineData("#if X\nclass A { }")]
    [InlineData("}}}} ;;;; ((((")]
    [InlineData("﻿class A { }\r\nclass B { }\n")]
    public void Garbage_and_odd_text_never_throws(string text)
    {
        var outcome = Parse(text);

        Assert.Null(outcome.Skip);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_tree_nested_past_the_depth_cap_is_skipped_too_deep()
    {
        var text = "class A { int F() { return " + new string('(', 200) + "1" + new string(')', 200) + "; } }";
        var parser = new CSharpParser(ParseLimits.Default with { MaxDepth = 50 });

        var outcome = Parse(text, parser);

        Assert.Equal("too-deep", outcome.Skip);
        Assert.Empty(outcome.Symbols);
    }

    [Fact, Trait("Lane", "unit")]
    public void More_symbols_than_the_ceiling_is_skipped_with_its_own_word()
    {
        var parser = new CSharpParser(ParseLimits.Default with { MaxSymbols = 3 });

        Assert.Equal("too-many-symbols", Parse("class A { void a() {} void b() {} void c() {} }", parser).Skip);
        Assert.Null(Parse("class A { void a() {} void b() {} }", parser).Skip);
    }

    [Fact, Trait("Lane", "unit")]
    public void More_call_sites_than_the_ceiling_is_skipped_with_its_own_word()
    {
        var parser = new CSharpParser(ParseLimits.Default with { MaxCallSites = 2 });

        Assert.Equal("too-many-call-sites", Parse("class A { void a() { x(); y(); z(); } }", parser).Skip);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_generated_file_past_the_old_symbol_and_call_site_ceilings_still_parses()
    {
        var methods = string.Join(' ', Enumerable.Range(0, 6_000).Select(i => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"void m{i}() {{ x(); x(); x(); x(); x(); }}")));

        var outcome = Parse("class A { " + methods + " }");

        Assert.Null(outcome.Skip);
        Assert.True(outcome.Symbols.Count >= 6_000);
    }

    [Fact, Trait("Lane", "unit")]
    public void One_files_parse_may_run_a_minute_on_a_loaded_box()
        => Assert.Equal(TimeSpan.FromSeconds(60), ParseLimits.Default.TimeLimit);

    [Fact, Trait("Lane", "unit")]
    public void A_parse_past_its_time_limit_is_skipped_timeout()
    {
        var parser = new CSharpParser(ParseLimits.Default with { TimeLimit = TimeSpan.Zero });

        Assert.Equal("timeout", Parse("class A { }", parser).Skip);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cancelled_caller_gets_the_cancellation_and_not_a_skip()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        _ = Assert.Throws<OperationCanceledException>(() => _parser.Parse("src/A.cs", "class A { }", cancelled.Token));
    }
}
