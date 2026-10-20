using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>A lens finding.</summary>
/// <param name="Finding">The finding.</param>
/// <param name="Severity">CRITICAL, HIGH, MEDIUM or LOW.</param>
/// <param name="Cite">The section.</param>
/// <param name="Text">What it says.</param>
/// <param name="Resolution">How it was resolved.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StopFinding(
    string Finding,
    string Severity,
    string Cite,
    string Text,
    string? Resolution);
