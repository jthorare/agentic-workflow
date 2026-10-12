using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Aw.Context.Testing.Fakes;

/// <summary>
/// Reads the GraphQL a read needs: one query operation, its variable definitions (with defaults), and a tree of
/// fields with aliases, arguments and sub-selections. Argument values are variables or scalar literals; objects and
/// lists come through variables. Fragments, directives, block strings and undeclared variables are refused with a
/// message saying so, never guessed.
/// </summary>
internal sealed class GraphQlReader(string text, JsonObject? variables)
{
    private readonly Dictionary<string, JsonNode?> _declared = new(StringComparer.Ordinal);
    private const int MaxDepth = 32;
    private int _at;
    private int _depth;

    public static IReadOnlyList<GraphQlField> RootFields(string query, JsonObject? variables) =>
        new GraphQlReader(query, variables).Operation();

    private char Peek => _at < text.Length ? text[_at] : '\0';

    private List<GraphQlField> Operation()
    {
        SkipIgnored();
        if (Peek != '{')
        {
            OperationHeader();
        }

        var fields = SelectionSet();
        SkipIgnored();
        return Peek == '\0' ? fields : throw FakeRequestException.Invalid("Only one operation per request; fragment definitions aren't supported by the fake Linear server.");
    }

    private void OperationHeader()
    {
        var kind = Name();
        if (string.Equals(kind, "mutation", StringComparison.Ordinal) || string.Equals(kind, "subscription", StringComparison.Ordinal))
        {
            throw FakeRequestException.Invalid($"The fake Linear server is read-only: '{kind}' operations aren't served.");
        }

        if (!string.Equals(kind, "query", StringComparison.Ordinal))
        {
            throw FakeRequestException.Invalid($"Expected 'query', 'mutation' or '{{' at the start of the document, found '{kind}'.");
        }

        SkipIgnored();
        if (char.IsLetter(Peek) || Peek == '_')
        {
            _ = Name();
            SkipIgnored();
        }

        if (Peek == '(')
        {
            VariableDefinitions();
            SkipIgnored();
        }

        RefuseDirective();
    }

    private void VariableDefinitions()
    {
        Expect('(');
        for (SkipIgnored(); Peek != ')'; SkipIgnored())
        {
            Expect('$');
            var name = Name();
            SkipIgnored();
            Expect(':');
            SkipType();
            SkipIgnored();
            JsonNode? fallback = null;
            if (Peek == '=')
            {
                _at++;
                SkipIgnored();
                fallback = Literal($"${name}");
            }

            _declared[name] = fallback;
        }

        _at++;
    }

    private void SkipType()
    {
        SkipIgnored();
        while (char.IsLetterOrDigit(Peek) || Peek is '_' or '[' or ']' or '!' || char.IsWhiteSpace(Peek))
        {
            _at++;
        }
    }

    private List<GraphQlField> SelectionSet()
    {
        if (++_depth > MaxDepth)
        {
            throw FakeRequestException.Invalid(string.Create(CultureInfo.InvariantCulture, $"Selection sets nest deeper than {MaxDepth} levels."));
        }

        Expect('{');
        var fields = new List<GraphQlField>();
        for (SkipIgnored(); Peek != '}'; SkipIgnored())
        {
            fields.Add(Field());
        }

        _at++;
        _depth--;
        return fields.Count > 0 ? fields : throw FakeRequestException.Invalid("An empty selection set '{ }' isn't valid GraphQL.");
    }

    private GraphQlField Field()
    {
        if (Peek == '.')
        {
            throw FakeRequestException.Invalid("Fragments ('...') aren't supported by the fake Linear server; select the fields directly.");
        }

        var key = Name();
        var name = key;
        SkipIgnored();
        if (Peek == ':')
        {
            _at++;
            SkipIgnored();
            name = Name();
            SkipIgnored();
        }

        var arguments = Peek == '(' ? Arguments() : new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        SkipIgnored();
        RefuseDirective();
        var selections = Peek == '{' ? SelectionSet() : null;
        return new GraphQlField(key, name, arguments, selections);
    }

    private Dictionary<string, JsonNode?> Arguments()
    {
        Expect('(');
        var arguments = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        for (SkipIgnored(); Peek != ')'; SkipIgnored())
        {
            var name = Name();
            SkipIgnored();
            Expect(':');
            SkipIgnored();
            arguments[name] = Peek == '$' ? Variable() : Literal(name);
        }

        _at++;
        return arguments;
    }

    private JsonNode? Variable()
    {
        _at++;
        var name = Name();
        if (!_declared.TryGetValue(name, out var fallback))
        {
            throw FakeRequestException.Invalid($"Variable \"${name}\" is not defined by the operation.");
        }

        return variables is not null && variables.TryGetPropertyValue(name, out var value) ? value?.DeepClone() : fallback?.DeepClone();
    }

    private JsonValue? Literal(string argument)
    {
        if (Peek is '{' or '[')
        {
            throw FakeRequestException.Invalid($"Argument '{argument}': pass object and list values through variables; the fake Linear server reads only scalar literals inline.");
        }

        return Peek == '"' ? JsonValue.Create(StringLiteral()) : ScalarLiteral();
    }

    private JsonValue? ScalarLiteral()
    {
        var start = _at;
        while (char.IsLetterOrDigit(Peek) || Peek is '_' or '-' or '.' or '+')
        {
            _at++;
        }

        var token = text[start.._at];
        return token switch
        {
            "true" => JsonValue.Create(value: true),
            "false" => JsonValue.Create(value: false),
            "null" => null,
            _ when int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) => JsonValue.Create(number),
            _ when double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var real) && double.IsFinite(real) => JsonValue.Create(real),
            _ when token.Length > 0 && (char.IsLetter(token[0]) || token[0] == '_') => JsonValue.Create(token),
            _ => throw FakeRequestException.Invalid(string.Create(CultureInfo.InvariantCulture, $"Unexpected '{token}{Peek}' at offset {start} of the query.")),
        };
    }

    private string StringLiteral()
    {
        if (text.AsSpan(_at).StartsWith("\"\"\"", StringComparison.Ordinal))
        {
            throw FakeRequestException.Invalid("Block strings (\"\"\") aren't supported by the fake Linear server; use a variable.");
        }

        var value = new StringBuilder();
        for (_at++; Peek != '"'; _at++)
        {
            if (Peek is '\0' or '\n')
            {
                throw FakeRequestException.Invalid("Unterminated string in the query.");
            }

            _ = Peek == '\\' ? value.Append(Escape()) : value.Append(Peek);
        }

        _at++;
        return value.ToString();
    }

    private char Escape()
    {
        _at++;
        return Peek switch
        {
            '"' or '\\' or '/' => Peek,
            'b' => '\b',
            'f' => '\f',
            'n' => '\n',
            'r' => '\r',
            't' => '\t',
            'u' when _at + 4 < text.Length && int.TryParse(text.AsSpan(_at + 1, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var code) => Unicode(code),
            _ => throw FakeRequestException.Invalid(string.Create(CultureInfo.InvariantCulture, $"Invalid escape in a string at offset {_at} of the query.")),
        };
    }

    private char Unicode(int code)
    {
        _at += 4;
        return (char)code;
    }

    private void RefuseDirective()
    {
        if (Peek == '@')
        {
            throw FakeRequestException.Invalid("Directives ('@') aren't supported by the fake Linear server.");
        }
    }

    private string Name()
    {
        var start = _at;
        while (char.IsLetterOrDigit(Peek) || Peek == '_')
        {
            _at++;
        }

        return _at > start ? text[start.._at] : throw FakeRequestException.Invalid(string.Create(CultureInfo.InvariantCulture, $"Expected a name at offset {start} of the query."));
    }

    private void Expect(char c)
    {
        if (Peek != c)
        {
            throw FakeRequestException.Invalid(string.Create(CultureInfo.InvariantCulture, $"Expected '{c}' at offset {_at} of the query."));
        }

        _at++;
    }

    private void SkipIgnored()
    {
        while (char.IsWhiteSpace(Peek) || Peek is ',' or '#')
        {
            if (Peek == '#')
            {
                while (Peek is not ('\n' or '\0'))
                {
                    _at++;
                }
            }
            else
            {
                _at++;
            }
        }
    }
}
