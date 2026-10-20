using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>One option of a decision.</summary>
/// <param name="Option">The option, for example D3.a.</param>
/// <param name="Answer">What choosing it means.</param>
/// <param name="Tradeoffs">What it costs and buys.</param>
/// <param name="Recommended">Whether it is the recommended one (named in text, never by colour only).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StopOption(
    string Option,
    string Answer,
    string Tradeoffs,
    bool Recommended);
