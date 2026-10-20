using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>A cross-spec edit and its checks.</summary>
/// <param name="Edit">The edit.</param>
/// <param name="Lane">The lane it is filed for.</param>
/// <param name="File">The target file.</param>
/// <param name="Heading">The target heading.</param>
/// <param name="Quote">The text it quotes.</param>
/// <param name="Change">What it changes it to.</param>
/// <param name="Row">Its row in "Rows to other lanes".</param>
/// <param name="Checks">DSN-XSPEC-001 to 003 for this edit.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StopContract(
    string Edit,
    string Lane,
    string File,
    string Heading,
    string Quote,
    string Change,
    int Row,
    IReadOnlyList<StopCheck> Checks);
