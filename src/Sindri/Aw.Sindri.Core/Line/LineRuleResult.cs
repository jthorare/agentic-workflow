using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>One rule's status in a run, with the file names it was missing when it did not run.</summary>
/// <param name="Rule">The rule id.</param>
/// <param name="Status">Its status.</param>
/// <param name="Missing">The names a not-run rule needed; empty otherwise.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LineRuleResult(string Rule, LineRuleStatus Status, IReadOnlyList<string> Missing);
