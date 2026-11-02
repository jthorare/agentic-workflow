namespace Aw.Sindri.Ui.Api.Sandbox;

/// <summary>How a launch's session ended (GET /sindri/launches/{id} 'ended'); null while it runs.</summary>
/// <param name="Reason">'exit', 'lost' or 'limit'.</param>
/// <param name="ExitCode">The exit code for an exit; null (omitted) otherwise.</param>
/// <param name="At">When the end was recorded.</param>
/// <param name="Kind">'memory' or 'wall' for a limit ending; null (omitted) otherwise.</param>
/// <param name="Limit">The limit's value (bytes for memory, seconds for wall); null (omitted) unless a limit ending.</param>
/// <param name="Peak">The peak of that limit in the same unit; null (omitted) unless a limit ending.</param>
internal sealed record LaunchEndView(
    string Reason,
    DateTimeOffset At,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? ExitCode = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? Kind = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] long? Limit = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] long? Peak = null);
