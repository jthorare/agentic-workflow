using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>A lens finding the owner accepted as a known risk.</summary>
/// <param name="Finding">The finding.</param>
/// <param name="Lens">The lens that raised it.</param>
/// <param name="Severity">CRITICAL, HIGH, MEDIUM or LOW.</param>
/// <param name="Cite">The section it is about.</param>
/// <param name="Text">What it says.</param>
/// <param name="Resolution">Why it was accepted.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StopAccepted(
    string Finding,
    string Lens,
    string Severity,
    string Cite,
    string Text,
    string Resolution);
