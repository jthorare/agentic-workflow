namespace Aw.Judge.Host.Commands;

/// <summary>AW_JUDGE_JEV_IMAGES as Compose read it: 0 or 1 wins over config.json's providers.jevImages.</summary>
/// <param name="Value">The variable's value, or null when it is unset.</param>
internal sealed record JevImagesVariable(string? Value);
