using System.Text.Json.Serialization;

namespace Aw.Sindri.Tests.Launch.Forms;

/// <summary>One unattended-open golden: the argument list, or the error code when the entry can't express the Step and nothing starts.</summary>
/// <param name="Agent">The catalog entry.</param>
/// <param name="Mode">The case's name.</param>
/// <param name="Argv">The arguments, absent when the open was refused.</param>
/// <param name="Error">The error code, absent when it started.</param>
internal sealed record ArgvStepGolden(
    string Agent,
    string Mode,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Argv,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Error);
