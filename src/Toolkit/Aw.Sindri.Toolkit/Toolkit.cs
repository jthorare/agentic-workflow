using System.Text.Json.Serialization;

namespace Aw.Sindri.Toolkit;

/// <summary>A toolkit's declaration: Toolkit.Named("contracts"), then UseJson, Needs and one Tool call per tool.</summary>
// why: the card names the entry point Toolkit.Named(...) and the package namespace is Aw.Sindri.Toolkit, so the type shares its namespace's last segment
#pragma warning disable MA0049
public sealed class Toolkit
{
    private readonly List<ResourceNeed> _needs = [];
    private readonly List<ToolDefinition> _tools = [];
    private JsonSerializerContext? _json;

    private Toolkit(string name)
    {
        Name = name;
    }

    /// <summary>The toolkit name.</summary>
    public string Name { get; }

    /// <summary>The declared resources, in declaration order.</summary>
    public IReadOnlyList<ResourceNeed> ResourceNeeds => _needs;

    /// <summary>The declared tools, in declaration order.</summary>
    public IReadOnlyList<ToolDefinition> Tools => _tools;

    /// <summary>Starts a declaration.</summary>
    /// <param name="name">Lower-case letters, digits and '-'.</param>
    /// <returns>The toolkit to add to.</returns>
    public static Toolkit Named(string name) => new(Sindri.Toolkit.Resources.CheckedName(name));

    /// <summary>Sets the version shown in the manifest (default 1.0.0).</summary>
    /// <param name="version">The version text.</param>
    /// <returns>This toolkit.</returns>
    public Toolkit WithVersion(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        Version = version;
        return this;
    }

    /// <summary>Sets the source-generated JSON context that holds every request and response type. It must set RespectNullableAnnotations and RespectRequiredConstructorParameters.</summary>
    /// <param name="context">The toolkit's context.</param>
    /// <returns>This toolkit.</returns>
    public Toolkit UseJson(JsonSerializerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _json = context;
        return this;
    }

    /// <summary>Declares resources.</summary>
    /// <param name="needs">Built with <see cref="Sindri.Toolkit.Resources"/>.</param>
    /// <returns>This toolkit.</returns>
    public Toolkit Needs(params ResourceNeed[] needs)
    {
        ArgumentNullException.ThrowIfNull(needs);
        _needs.AddRange(needs);
        return this;
    }

    /// <summary>Adds a typed tool. The request type gives the input schema.</summary>
    /// <typeparam name="TRequest">The request record.</typeparam>
    /// <typeparam name="TResponse">The response record.</typeparam>
    /// <param name="name">The tool name.</param>
    /// <param name="description">What it does.</param>
    /// <param name="handler">Runs the tool.</param>
    /// <returns>This toolkit.</returns>
    public Toolkit Tool<TRequest, TResponse>(string name, string description, Func<TRequest, ToolkitContext, CancellationToken, Task<TResponse>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        var json = _json ?? throw new ToolkitException(ToolkitErrors.InvalidDeclaration, $"Call UseJson(<your JsonSerializerContext>) before adding tool '{name}'.");
        if (!IsToolName(name))
        {
            throw new ToolkitException(ToolkitErrors.InvalidDeclaration, $"'{name}' is not a tool name: use lower-case letters, digits and '_', starting with a letter.");
        }

        if (_tools.Exists(t => string.Equals(t.Name, name, StringComparison.Ordinal)))
        {
            throw new ToolkitException(ToolkitErrors.InvalidDeclaration, $"The toolkit already has a tool named '{name}'.");
        }

        _tools.Add(ToolFactory.Create(json, name, description, handler));
        return this;
    }

    /// <summary>The manifest, toolkit.json, as indented JSON.</summary>
    /// <returns>The JSON text.</returns>
    public string Manifest() => ManifestWriter.Write(Name, Version, _needs, _tools);

    /// <summary>The version shown in the manifest and to MCP clients.</summary>
    internal string Version { get; private set; } = "1.0.0";

    private static bool IsToolName(string name) =>
        !string.IsNullOrEmpty(name) && name[0] is >= 'a' and <= 'z' && name.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_');
}
