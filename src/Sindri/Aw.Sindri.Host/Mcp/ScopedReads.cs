using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Context.Core;
using Aw.Context.Core.Facade;
using Aw.Context.Core.ScopeGraph;
using Aw.Kernel;

namespace Aw.Sindri.Host.Mcp;

/// <summary>
/// The scoped reads of a scoping Step's session (issue 1005): context_node, context_neighbors and context_query check the node they are asked about against the run's graph, which the engine keeps per workflow
/// instance, and log the read under the run. The session names its run with X-Sindri-Instance and X-Sindri-Run, set when the agent is launched; a call without them reads as it always did.
/// A node outside the graph is refused (SND-CONTEXT-154) unless the call passes <c lang="text">outside: true</c> and a <c lang="text">reason</c>.
/// </summary>
internal static class ScopedReads
{
    private const string OutsideName = "outside";
    private const string ReasonName = "reason";

    /// <summary>The 'outside' property of a scoped tool's schema; a new node each call, since a JSON node has one parent.</summary>
    /// <returns>The property.</returns>
    internal static JsonObject OutsideProperty() => ContextTools.Property("boolean", "Set true, with a reason, to read a node outside this run's scoping graph; ignored by a session that has no scoping run");

    /// <summary>The 'reason' property of a scoped tool's schema; a new node each call.</summary>
    /// <returns>The property.</returns>
    internal static JsonObject ReasonProperty() => ContextTools.Property("string", "Why this read steps outside the scoping graph; required with outside");

    /// <summary>The call's arguments without 'outside' and 'reason', which the tool's own binder doesn't know, and those two.</summary>
    /// <param name="Rest">The other arguments.</param>
    /// <param name="Outside">True when the call asks to step outside the graph.</param>
    /// <param name="Reason">The reason, or null.</param>
    internal sealed record Split(IDictionary<string, JsonElement>? Rest, bool Outside, string? Reason);

    /// <summary>Splits the scoped arguments off a call.</summary>
    /// <param name="tool">The tool's name, for the message.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <returns>The split; SND-CONTEXT-022 for a wrong type, or 'outside' without a reason.</returns>
    internal static Result<Split> Separate(string tool, IDictionary<string, JsonElement>? arguments)
    {
        if (arguments is null || !(arguments.ContainsKey(OutsideName) || arguments.ContainsKey(ReasonName)))
        {
            return new Split(arguments, Outside: false, Reason: null);
        }

        var rest = arguments.Where(a => a.Key is not (OutsideName or ReasonName)).ToDictionary(a => a.Key, a => a.Value, StringComparer.Ordinal);
        var outside = Flag(tool, arguments);
        var why = Reason(tool, arguments);
        if (outside.IsError || why.IsError)
        {
            return outside.IsError ? outside.Error : why.Error;
        }

        return outside.Value && string.IsNullOrWhiteSpace(why.Value)
            ? ContextErrors.McpArgumentsInvalid.With($"{tool}: 'outside: true' needs a 'reason' (fix: say why the read leaves the scoping graph)")
            : new Split(rest, outside.Value, why.Value);
    }

    // 'outside' is absent, null, true or false.
    private static Result<bool> Flag(string tool, IDictionary<string, JsonElement> arguments)
    {
        var kind = arguments.TryGetValue(OutsideName, out var value) ? value.ValueKind : JsonValueKind.Undefined;
        return kind is JsonValueKind.Undefined or JsonValueKind.Null or JsonValueKind.True or JsonValueKind.False
            ? kind == JsonValueKind.True
            : ContextErrors.McpArgumentsInvalid.With($"{tool}: 'outside' must be true or false (fix: see the tool's input schema)");
    }

    // 'reason' is absent, null or a string.
    private static Result<string?> Reason(string tool, IDictionary<string, JsonElement> arguments)
    {
        if (!arguments.TryGetValue(ReasonName, out var value) || value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return Kernel.Results.Success<string?>(value: null);
        }

        return value.ValueKind == JsonValueKind.String
            ? Kernel.Results.Success(value.GetString())
            : ContextErrors.McpArgumentsInvalid.With($"{tool}: 'reason' must be a string (fix: see the tool's input schema)");
    }

    /// <summary>Checks a read against the session's run graph and logs it. A session with no run, or an instance with no remembered graph, is not scoped.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="tool">The tool's name.</param>
    /// <param name="node">The node the call asks about.</param>
    /// <param name="scoped">The split arguments.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>Null to go on with the read, else the refusal (SND-CONTEXT-154, or the store's error).</returns>
    internal static async ValueTask<Error?> Check(IServiceProvider services, string tool, string node, Split scoped, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scoped);
        var headers = services.GetRequiredService<IHttpContextAccessor>().HttpContext?.Request.Headers;
        var instance = headers?[SindriHeaders.Instance].ToString();
        var run = headers?[SindriHeaders.Run].ToString();
        if (string.IsNullOrWhiteSpace(instance) || string.IsNullOrWhiteSpace(run))
        {
            return null;
        }

        var read = new GraphRead(node, tool, scoped.Outside, scoped.Reason, services.GetRequiredService<TimeProvider>().GetUtcNow());
        var checkedRead = await services.GetRequiredService<IContextEngine>().ReadInScopeGraph(instance, run, read, cancellationToken).ConfigureAwait(false);
        return checkedRead.IsError && checkedRead.Error.Code != ContextErrors.ScopeGraphUnavailable.Code ? checkedRead.Error : null;
    }
}
