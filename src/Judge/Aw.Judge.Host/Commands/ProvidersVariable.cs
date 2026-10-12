namespace Aw.Judge.Host.Commands;

/// <summary>AW_JUDGE_PROVIDERS as Compose read it: the providers allowlist when argv has no '--providers' flag.</summary>
/// <param name="Value">The variable's value, or null when it is unset.</param>
internal sealed record ProvidersVariable(string? Value);
