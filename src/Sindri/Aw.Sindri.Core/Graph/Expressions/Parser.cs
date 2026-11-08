using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Graph.Expressions;

/// <summary>Recursive descent over the tokens. Precedence, tightest first: not, comparison, and, or.</summary>
internal sealed class Parser
{
    private readonly List<Tokenizer.Token> _tokens;
    private readonly List<string> _references = [];
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private int _position;

    private Parser(List<Tokenizer.Token> tokens)
    {
        _tokens = tokens;
    }

    private Tokenizer.Token Peek => _tokens[_position];

    internal static bool TryParse(string text, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Evaluator.Node? root, out IReadOnlyList<string> references, out string? error)
    {
        root = null;
        references = [];
        if (!Tokenizer.TryTokenize(text, out var tokens, out error))
        {
            return false;
        }

        var parser = new Parser(tokens);
        try
        {
            root = parser.ParseOr();
            if (parser.Peek.Kind != Tokenizer.Kind.End)
            {
                throw Fail(parser.Peek, "unexpected " + Tokenizer.Describe(parser.Peek) + " after a complete condition; join conditions with 'and' or 'or'");
            }
        }
        catch (ParseException failure)
        {
            root = null;
            error = failure.Message;
            return false;
        }

        references = parser._references;
        return true;
    }

    private static ParseException Fail(Tokenizer.Token at, string message) => new(Tokenizer.At(at.Column, message));

    private static bool IsComparison(Tokenizer.Kind kind) =>
        kind is Tokenizer.Kind.Eq or Tokenizer.Kind.Ne or Tokenizer.Kind.Lt or Tokenizer.Kind.Le or Tokenizer.Kind.Gt or Tokenizer.Kind.Ge;

    private Tokenizer.Token Next()
    {
        var token = _tokens[_position];
        if (token.Kind != Tokenizer.Kind.End)
        {
            _position++;
        }

        return token;
    }

    private void Expect(Tokenizer.Kind kind, string symbol)
    {
        var token = Next();
        if (token.Kind != kind)
        {
            throw Fail(token, "expected '" + symbol + "' but found " + Tokenizer.Describe(token));
        }
    }

    private string Track(string name)
    {
        if (_seen.Add(name))
        {
            _references.Add(name);
        }

        return name;
    }

    private Evaluator.Node ParseOr()
    {
        var left = ParseAnd();
        while (Peek.Kind == Tokenizer.Kind.Or)
        {
            _ = Next();
            left = new Evaluator.Or(left, ParseAnd());
        }

        return left;
    }

    private Evaluator.Node ParseAnd()
    {
        var left = ParseComparison();
        while (Peek.Kind == Tokenizer.Kind.And)
        {
            _ = Next();
            left = new Evaluator.And(left, ParseComparison());
        }

        return left;
    }

    private Evaluator.Node ParseComparison()
    {
        var left = ParseNot();
        if (!IsComparison(Peek.Kind))
        {
            return left;
        }

        var op = Next().Kind;
        return new Evaluator.Compare(op, left, ParseNot());
    }

    private Evaluator.Node ParseNot()
    {
        if (Peek.Kind != Tokenizer.Kind.Not)
        {
            return ParsePrimary();
        }

        _ = Next();
        return new Evaluator.Not(ParseNot());
    }

    private static Evaluator.Literal? LiteralOf(Tokenizer.Token token)
    {
        if (token.Kind is Tokenizer.Kind.Str or Tokenizer.Kind.Num)
        {
            return new Evaluator.Literal(token.Value);
        }

        if (token.Kind is Tokenizer.Kind.True or Tokenizer.Kind.False)
        {
            return new Evaluator.Literal(JsonValue.Create(value: token.Kind == Tokenizer.Kind.True));
        }

        return token.Kind == Tokenizer.Kind.Null ? new Evaluator.Literal(Value: null) : null;
    }

    private Evaluator.Node ParsePrimary()
    {
        var token = Next();
        var literal = LiteralOf(token);
        if (literal is not null)
        {
            return literal;
        }

        if (token.Kind == Tokenizer.Kind.Ref)
        {
            return ParseReference(token);
        }

        if (token.Kind == Tokenizer.Kind.Exists)
        {
            return ParseExists();
        }

        if (token.Kind != Tokenizer.Kind.LParen)
        {
            throw Fail(token, "expected a value, a reference or '(' but found " + Tokenizer.Describe(token));
        }

        var inner = ParseOr();
        Expect(Tokenizer.Kind.RParen, ")");
        return inner;
    }

    private Evaluator.Reference ParseReference(Tokenizer.Token token)
    {
        if (Peek.Kind == Tokenizer.Kind.LParen)
        {
            throw Fail(Peek, "'" + token.Text + "' is not a function; the only function is exists(reference)");
        }

        return new Evaluator.Reference(Track(token.Text));
    }

    private Evaluator.Exists ParseExists()
    {
        Expect(Tokenizer.Kind.LParen, "(");
        var argument = Next();
        if (argument.Kind != Tokenizer.Kind.Ref)
        {
            throw Fail(argument, "exists() takes one reference, like exists(node.field), but found " + Tokenizer.Describe(argument));
        }

        Expect(Tokenizer.Kind.RParen, ")");
        return new Evaluator.Exists(Track(argument.Text));
    }

    private sealed class ParseException : Exception
    {
        public ParseException()
        {
        }

        public ParseException(string message)
            : base(message)
        {
        }

        public ParseException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
