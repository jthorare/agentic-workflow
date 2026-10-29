namespace Aw.Sindri.Ui.Api.Sandbox;

/// <summary>One counted denial target.</summary>
/// <param name="Kind">'network', 'policy' or 'path'.</param>
/// <param name="Target">The host, path or tool name.</param>
/// <param name="Rule">The rule id.</param>
/// <param name="Count">How many times.</param>
/// <param name="First">The first time.</param>
/// <param name="Last">The latest time.</param>
/// <param name="Fix">'allow' (a host, or a home path not bound), 'never' (the permanent deny list) or 'own-terminal' (policy); null (omitted) from a server before the field.</param>
/// <param name="Reason">Why, for 'never'; null (omitted) otherwise.</param>
internal sealed record DenialEntryView(
    string Kind,
    string Target,
    string Rule,
    int Count,
    DateTimeOffset First,
    DateTimeOffset Last,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? Fix = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? Reason = null);
