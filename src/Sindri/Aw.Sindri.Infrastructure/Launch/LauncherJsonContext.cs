using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The source-generated context for launcher.json's internal records (an Infrastructure project keeps its own for a wire format the host can't name).</summary>
[JsonSerializable(typeof(LauncherDocument))]
[JsonSerializable(typeof(ReportedIdSection))]
[JsonSerializable(typeof(Core.Launch.CaptureRule))]
[JsonSerializable(typeof(ClaudeSettingsDocument))]
[JsonSerializable(typeof(ClaudeHooks))]
[JsonSerializable(typeof(McpConfigDocument))]
[JsonSerializable(typeof(TerminalTemplatesFile))]
[JsonSerializable(typeof(CodexModelsDocument))]
internal sealed partial class LauncherJsonContext : JsonSerializerContext;
