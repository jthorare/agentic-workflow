using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Aw.Sindri.Infrastructure.Workflow.Code;

/// <summary>Roslyn over a fixed reference set: the base class library, System.Text.Json and System.Linq.</summary>
internal sealed class CodeCompiler : ICodeCompiler
{
    // why: the wrapper puts the body on its own lines under '#line 1', so a diagnostic's line and column are the body's own
    private const string Head = "#nullable enable\nusing System;\nusing System.Collections.Generic;\nusing System.Linq;\nusing System.Text.Json;\nusing System.Text.Json.Nodes;\nusing System.Text.RegularExpressions;\npublic static class SindriCodeBody\n{\n    public static object? Run(JsonNode? input)\n    {\n#line 1 \"code\"\n";
    private const string Tail = "\n#line hidden\n    }\n}\n";

    private static readonly string[] _assemblyNames =
    [
        "System.Private.CoreLib", "System.Runtime", "System.Collections", "System.Linq", "System.Memory", "System.Text.Json", "System.Text.RegularExpressions",
    ];

    private static readonly Lazy<(ImmutableArray<MetadataReference> References, string Set)> _references = new(Load);

    /// <inheritdoc />
    public string Fingerprint => _references.Value.Set;

    /// <inheritdoc />
    public CodeCompilation Compile(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var tree = CSharpSyntaxTree.ParseText(Head + body + Tail);
        var compilation = CSharpCompilation.Create(
            "sindri-code-" + Guid.NewGuid().ToString("N"),
            [tree],
            _references.Value.References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release, nullableContextOptions: NullableContextOptions.Enable));
        using var stream = new MemoryStream();
        var emitted = compilation.Emit(stream);
        if (emitted.Success)
        {
            return new CodeCompilation([], stream.ToArray());
        }

        var errors = emitted.Diagnostics
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .Select(static d =>
            {
                var at = d.Location.GetMappedLineSpan().StartLinePosition;
                return new CodeDiagnostic(at.Line + 1, at.Character + 1, d.Id, d.GetMessage(CultureInfo.InvariantCulture));
            })
            .ToList();
        return new CodeCompilation(errors, Assembly: null);
    }

    private static (ImmutableArray<MetadataReference>, string) Load()
    {
        var trusted = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var references = ImmutableArray.CreateBuilder<MetadataReference>();
        List<string> ids = [];
        foreach (var name in _assemblyNames)
        {
            var path = trusted.FirstOrDefault(p => string.Equals(Path.GetFileNameWithoutExtension(p), name, StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"the runtime has no assembly '{name}' to compile against");
            references.Add(MetadataReference.CreateFromFile(path));
            ids.Add(name + "@" + System.Reflection.AssemblyName.GetAssemblyName(path).Version);
        }

        return (references.ToImmutable(), string.Join(';', ids));
    }
}
