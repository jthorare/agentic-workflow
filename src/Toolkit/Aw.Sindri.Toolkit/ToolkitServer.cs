using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Aw.Sindri.Toolkit;

/// <summary>The MCP handlers of one toolkit: list its tools and call one of them, mapping every failure to a tool error that carries no stack and no resource value.</summary>
internal sealed class ToolkitServer(Toolkit toolkit, Func<string, string?> environment)
{
    private readonly ToolkitContext _context = new(toolkit.ResourceNeeds, environment);

    private readonly string[] _secrets =
    [
        .. toolkit.ResourceNeeds
            .Where(n => n.Kind == ResourceKind.Connection)
            .Select(n => environment(n.Variable))
            .OfType<string>()
            .Where(value => value.Length >= 4),
    ];

    public McpServerOptions Options() => new()
    {
        ServerInfo = new Implementation { Name = toolkit.Name, Version = toolkit.Version },
        Capabilities = new ServerCapabilities { Tools = new ToolsCapability() },
        Handlers = new McpServerHandlers
        {
            ListToolsHandler = List,
            CallToolHandler = CallAsync,
        },
    };

    private ValueTask<ListToolsResult> List(RequestContext<ListToolsRequestParams> request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new ListToolsResult
        {
            Tools =
            [
                .. toolkit.Tools.Select(t => new Tool
                {
                    Name = t.Name,
                    Description = t.Description,
                    InputSchema = ToolResults.Element(t.InputSchema),
                    OutputSchema = ToolResults.Element(t.OutputSchema),
                }),
            ],
        });

    private async ValueTask<CallToolResult> CallAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken)
    {
        var name = request.Params?.Name ?? string.Empty;
        var tool = toolkit.Tools.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.Ordinal));
        if (tool is null)
        {
            return ToolResults.Failure(ToolkitErrors.InvalidCall, $"This toolkit has no tool named '{name}'.");
        }

        try
        {
            var arguments = ToObject(request.Params?.Arguments);
            var response = await tool.Invoke(arguments, _context, cancellationToken).ConfigureAwait(false);
            return ToolResults.Success(name, response);
        }
        catch (ToolkitException ex)
        {
            return ToolResults.Failure(ex.Definition, Scrub(ex.Message));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // the boundary of a tool call: any other exception is a tool failure, reported without its message, type or stack
        catch (Exception)
#pragma warning restore CA1031
        {
            return ToolResults.Failure(ToolkitErrors.ToolFailed, $"Tool '{name}' failed.");
        }
    }

    private static JsonElement ToObject(IDictionary<string, JsonElement>? arguments)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var pair in arguments ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal))
            {
                writer.WritePropertyName(pair.Key);
                pair.Value.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(buffer.ToArray());
        return document.RootElement.Clone();
    }

    private string Scrub(string message)
    {
        var scrubbed = message;
        foreach (var secret in _secrets)
        {
            scrubbed = scrubbed.Replace(secret, "[redacted]", StringComparison.Ordinal);
        }

        return scrubbed;
    }
}
