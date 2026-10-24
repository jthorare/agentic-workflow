using System.Text.Json.Serialization;

namespace Aw.Judge.Host.Commands;

/// <summary>The 'prompt-sort' stdout record (hook contract: prompt-sort.sh reads '.context'); printed only when a scaffold fires.</summary>
/// <param name="Context">The note a firing scaffold adds to the prompt.</param>
internal sealed record SortPromptOutput([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Context);
