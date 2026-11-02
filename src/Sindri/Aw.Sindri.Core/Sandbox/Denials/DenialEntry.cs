using System.Text.Json.Serialization;
using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Core.Sandbox.Denials;

/// <summary>One counted denial target.</summary>
/// <param name="Kind">Network, policy or path.</param>
/// <param name="Target">The host, path or tool name.</param>
/// <param name="Rule">The rule id.</param>
/// <param name="Count">How many times.</param>
/// <param name="First">The first time.</param>
/// <param name="Last">The latest time.</param>
/// <param name="Fix">What the owner can do about it; set when the API serves the entry, absent in the counter file.</param>
/// <param name="Reason">Why it is never allowed, in the owner's words; set only with <see cref="DenialFix.Never"/>.</param>
public sealed record DenialEntry(
    DenialKind Kind, string Target, string Rule, int Count, DateTimeOffset First, DateTimeOffset Last,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DenialFix? Fix = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Reason = null);
