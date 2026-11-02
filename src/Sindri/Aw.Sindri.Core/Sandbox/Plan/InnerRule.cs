namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>One target-neutral rule of the agent CLI's own policy; SB-5a renders it for each CLI.</summary>
/// <param name="Effect">Allow or deny.</param>
/// <param name="Tool">The tool: Read, Grep, Edit, Write, Glob, Bash or an MCP tool pattern.</param>
/// <param name="Pattern">The path, command prefix or tool pattern; empty for the whole tool.</param>
public sealed record InnerRule(InnerRuleEffect Effect, string Tool, string Pattern);
