namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>One self-check probe (sandbox spec §6): a script that runs inside the planned sandbox and exits 0 when the denial or grant holds.</summary>
/// <param name="Reason">The -016 reason when it fails ('fs-deny', 'git-ref', ...).</param>
/// <param name="Script">The sh script.</param>
/// <param name="Shell">The shell that runs it: sh, or bash for the hook canary (lib/failopen.sh is bash).</param>
/// <param name="Judge">Reads what a script that exited 0 printed: null when the canary holds, else what was observed. Null means the exit code alone decides.</param>
internal sealed record SandboxCanary(string Reason, string Script, Func<ProcessResult, string?>? Judge = null, string Shell = "/bin/sh");
