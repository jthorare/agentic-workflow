namespace Aw.Context.Core.Glossary;

/// <summary>The default code-noise words: common naming filler, English glue and C# and TypeScript keywords.</summary>
internal static class GlossaryStoplist
{
    internal static readonly IReadOnlySet<string> Default = new HashSet<string>(
        [
            "get", "set", "add", "new", "init", "impl", "service", "handler", "manager", "controller", "util", "utils",
            "helper", "base", "test", "tests", "async", "dto", "model", "view", "request", "response", "the", "and", "for",
            "with", "from", "into",
            "abstract", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue", "decimal",
            "default", "delegate", "double", "else", "enum", "event", "explicit", "extern", "false", "finally", "fixed",
            "float", "foreach", "goto", "implicit", "int", "interface", "internal", "lock", "long", "namespace", "null",
            "object", "operator", "out", "override", "params", "private", "protected", "public", "readonly", "ref", "return",
            "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw",
            "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void",
            "volatile", "while", "var", "dynamic", "record",
            "function", "let", "type", "import", "export", "await", "number", "boolean", "any", "never", "unknown",
            "undefined", "declare", "module", "extends", "implements", "instanceof", "yield", "keyof", "constructor",
        ],
        StringComparer.Ordinal);
}
